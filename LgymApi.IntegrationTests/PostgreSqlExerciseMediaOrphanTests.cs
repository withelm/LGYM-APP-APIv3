using System.Collections.Concurrent;
using System.Security.Cryptography;
using FluentAssertions;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Repositories;
using LgymApi.Application.Features.Exercise;
using LgymApi.Application.WorkoutProgress.Errors;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.UnitOfWork;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("PostgreSql")]
public sealed class PostgreSqlExerciseMediaOrphanTests : PostgreSqlIntegrationTestBase
{
    private WebApplicationFactory<Program>? _mediaFactory;
    private readonly ControlledStorage _storage = new();
    private int _uncertainCommit;
    private static readonly byte[] Gif = Convert.FromHexString("47494638396101000100800000000000ffffff21ff0b4e45545343415045322e30030100000021f904000a0000002c000000000100010000020244010021f90400140000002c00000000010001000002024c01003b");

    [TearDown]
    public async Task DisposeMediaHostAsync()
    {
        if (_mediaFactory is not null) await _mediaFactory.DisposeAsync();
        _mediaFactory = null; _storage.Reset(); _uncertainCommit = 0;
    }

    private IServiceScope Scope()
    {
        _mediaFactory ??= Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<IExerciseMediaStorage>(_storage);
            services.AddScoped<IUnitOfWork>(provider => new UncertainCommitUnitOfWork(new EfUnitOfWork(provider.GetRequiredService<AppDbContext>()), () => Interlocked.Exchange(ref _uncertainCommit, 0) == 1));
        }));
        return _mediaFactory.Services.CreateScope();
    }

    private static AuthenticatedAccountContext Actor() => new(Id<AccountReference>.New(), null, [], [AuthConstants.Permissions.ManageGlobalExercises], false, false);
    private static ExerciseMediaSourceInput Source() => new("image/gif", Gif.Length, Convert.ToHexStringLower(SHA256.HashData(Gif)));
    private static ChangeExerciseMediaInput Replacement() => new(Id<ExerciseMediaOperationReference>.New(), null, Source(), false, null);

    private async Task<Orphan> SeedOrphanAsync()
    {
        var actor = Actor(); var input = Replacement();
        using var scope = Scope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var exercise = new Exercise { Id = Id<Exercise>.New(), Name = "Orphan race fixture", BodyPart = BodyParts.Chest, Description = "Keep metadata", EloFormula = ExerciseEloFormula.VolumeWeighted };
        db.Exercises.Add(exercise); await db.SaveChangesAsync();
        _storage.FailAfterPublish = true;
        var interrupted = await scope.ServiceProvider.GetRequiredService<IExerciseMediaService>().ChangeAsync(actor, exercise.Id, input, Gif, default);
        interrupted.IsFailure.Should().BeTrue(); interrupted.Error.HttpStatusCode.Should().Be(503);
        using var expiration = Scope(); var expiredDb = expiration.ServiceProvider.GetRequiredService<AppDbContext>();
        var ledger = await expiredDb.ExerciseMediaUploads.SingleAsync(x => x.OperationId == input.OperationId);
        ledger.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
        var fence = await expiredDb.ExerciseMediaObjectFences.SingleAsync(x => x.ObjectKey == ledger.ObjectKey);
        fence.CreatedAt = DateTimeOffset.UtcNow.AddDays(-2); await expiredDb.SaveChangesAsync();
        return new(exercise, actor, input, fence.ObjectKey, _storage.Objects[fence.ObjectKey].ToArray());
    }

    [Test]
    public async Task PublicationReservationWinsStaleCleanup_AcrossActorAndOperation_DeletesNothingAndReplays()
    {
        var orphan = await SeedOrphanAsync(); using var cleaner = Scope(); var db = cleaner.ServiceProvider.GetRequiredService<AppDbContext>();
        using var gate = new SaveGate(db, () => db.ChangeTracker.Entries<ExerciseMediaObjectFence>().Any(x => x.Entity.IsRetired));
        var externalWriteEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseExternalWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _storage.BeforePublish = async (_, _, cancellationToken) => { externalWriteEntered.TrySetResult(); await releaseExternalWrite.Task.WaitAsync(cancellationToken); };
        var cleanup = Task.Run(() => cleaner.ServiceProvider.GetRequiredService<IExerciseMediaService>().CleanupAsync(Actor(), default));
        Task? publication = null;
        try
        {
            await gate.Arrived.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var nextActor = Actor(); var input = Replacement();
            using var publisher = Scope();
            var publishing = publisher.ServiceProvider.GetRequiredService<IExerciseMediaService>().ChangeAsync(nextActor, orphan.Exercise.Id, input, Gif, default);
            publication = publishing;
            await externalWriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            using (var reserved = Scope())
            {
                var database = reserved.ServiceProvider.GetRequiredService<AppDbContext>();
                (await database.ExerciseMediaUploads.SingleAsync(x => x.OperationId == input.OperationId)).Status.Should().Be("publishing");
                (await database.Exercises.SingleAsync(x => x.Id == orphan.Exercise.Id)).Image.Should().BeNull();
            }
            gate.Release(); (await cleanup.WaitAsync(TimeSpan.FromSeconds(30))).IsSuccess.Should().BeTrue();
            _storage.DeleteCalls.Should().Be(0, "the reservation itself must fence stale cleanup before the external PUT or binding can finish");
            releaseExternalWrite.TrySetResult();
            var bound = await publishing.WaitAsync(TimeSpan.FromSeconds(30)); bound.IsSuccess.Should().BeTrue();
            using var replayScope = Scope();
            var replay = await replayScope.ServiceProvider.GetRequiredService<IExerciseMediaService>().ChangeAsync(nextActor, orphan.Exercise.Id, input, null, default);
            replay.IsSuccess.Should().BeTrue(); replay.Value.Image.Should().Be(bound.Value.Image);
            _storage.Objects.Should().ContainKey(orphan.Key);
        }
        finally
        {
            gate.Release(); releaseExternalWrite.TrySetResult();
            await cleanup.WaitAsync(TimeSpan.FromSeconds(30));
            if (publication is not null) await publication.WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    [Test]
    public async Task CleanupClaimWins_DelayedDeleteAndRetryCannotPermitSameKeyReuse()
    {
        var orphan = await SeedOrphanAsync(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _storage.BeforeDelete = async (attempt, _, cancellationToken) => { if (attempt == 1) { entered.TrySetResult(); await release.Task.WaitAsync(cancellationToken); } };
        using var firstScope = Scope();
        var first = firstScope.ServiceProvider.GetRequiredService<IExerciseMediaService>().CleanupAsync(Actor(), default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            using (var retryScope = Scope()) (await retryScope.ServiceProvider.GetRequiredService<IExerciseMediaService>().CleanupAsync(Actor(), default)).IsSuccess.Should().BeTrue();
            var writes = _storage.PublishCalls;
            using var publisher = Scope();
            var denied = await publisher.ServiceProvider.GetRequiredService<IExerciseMediaService>().ChangeAsync(Actor(), orphan.Exercise.Id, Replacement(), Gif, default);
            denied.IsFailure.Should().BeTrue(); denied.Error.HttpStatusCode.Should().Be(409);
            _storage.PublishCalls.Should().Be(writes, "a retired key cannot be rewritten even after a successful retry DELETE");
            release.TrySetResult(); await first.WaitAsync(TimeSpan.FromSeconds(30));
            _storage.Objects.Should().NotContainKey(orphan.Key);
            using var check = Scope(); var database = check.ServiceProvider.GetRequiredService<AppDbContext>();
            (await database.ExerciseMediaObjectFences.SingleAsync(x => x.ObjectKey == orphan.Key)).IsRetired.Should().BeTrue();
            (await database.Exercises.SingleAsync(x => x.Id == orphan.Exercise.Id)).Image.Should().BeNull();
            var original = await check.ServiceProvider.GetRequiredService<IExerciseMediaService>().ChangeAsync(orphan.Actor, orphan.Exercise.Id, orphan.Input, null, default);
            original.IsFailure.Should().BeTrue(); original.Error.HttpStatusCode.Should().Be(409);
        }
        finally { release.TrySetResult(); await first.WaitAsync(TimeSpan.FromSeconds(30)); }
    }

    [Test]
    public async Task ConcurrentCleanupClaims_OneCasLoserMakesZeroDeleteCalls()
    {
        await SeedOrphanAsync(); using var a = Scope(); using var b = Scope();
        var aDb = a.ServiceProvider.GetRequiredService<AppDbContext>(); var bDb = b.ServiceProvider.GetRequiredService<AppDbContext>();
        using var aGate = new SaveGate(aDb, () => aDb.ChangeTracker.Entries<ExerciseMediaObjectFence>().Any(x => x.Entity.IsRetired));
        using var bGate = new SaveGate(bDb, () => bDb.ChangeTracker.Entries<ExerciseMediaObjectFence>().Any(x => x.Entity.IsRetired));
        var first = Task.Run(() => a.ServiceProvider.GetRequiredService<IExerciseMediaService>().CleanupAsync(Actor(), default));
        var second = Task.Run(() => b.ServiceProvider.GetRequiredService<IExerciseMediaService>().CleanupAsync(Actor(), default));
        try
        {
            await Task.WhenAll(aGate.Arrived.Task, bGate.Arrived.Task).WaitAsync(TimeSpan.FromSeconds(30));
            aGate.Release(); (await first.WaitAsync(TimeSpan.FromSeconds(30))).Value.Cleaned.Should().Be(1);
            bGate.Release(); (await second.WaitAsync(TimeSpan.FromSeconds(30))).Value.Cleaned.Should().Be(0);
            _storage.DeleteCalls.Should().Be(1);
        }
        finally { aGate.Release(); bGate.Release(); await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(30)); }
    }

    [Test]
    public async Task FailedDelete_RetryAndLatePutRemainTrackedByPermanentRetirement()
    {
        var orphan = await SeedOrphanAsync(); _storage.FailNextDelete = true;
        using (var first = Scope()) (await first.ServiceProvider.GetRequiredService<IExerciseMediaService>().CleanupAsync(Actor(), default)).Value.Remaining.Should().Be(1);
        _storage.Objects.Should().ContainKey(orphan.Key);
        using (var retry = Scope()) (await retry.ServiceProvider.GetRequiredService<IExerciseMediaService>().CleanupAsync(Actor(), default)).Value.Cleaned.Should().Be(1);
        _storage.Objects[orphan.Key] = orphan.Bytes.ToArray(); // A previously uncertain PUT completes after the successful DELETE.
        using (var reconcile = Scope()) (await reconcile.ServiceProvider.GetRequiredService<IExerciseMediaService>().CleanupAsync(Actor(), default)).Value.Cleaned.Should().Be(1);
        _storage.Objects.Should().NotContainKey(orphan.Key); _storage.DeleteCalls.Should().Be(3);
        using var check = Scope();
        (await check.ServiceProvider.GetRequiredService<AppDbContext>().ExerciseMediaObjectFences.SingleAsync(x => x.ObjectKey == orphan.Key)).IsRetired.Should().BeTrue();
    }

    [Test]
    public async Task CancellationAfterDurableClaim_CannotUnfenceObjectAndRetryIsSafe()
    {
        var orphan = await SeedOrphanAsync(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var pause = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _storage.BeforeDelete = async (_, _, cancellationToken) => { entered.TrySetResult(); await pause.Task.WaitAsync(cancellationToken); };
        using var cancellation = new CancellationTokenSource(); using var cleaner = Scope();
        var pending = cleaner.ServiceProvider.GetRequiredService<IExerciseMediaService>().CleanupAsync(Actor(), cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(30)); cancellation.Cancel();
        await Assert.CatchAsync<OperationCanceledException>(async () => await pending);
        using (var check = Scope()) (await check.ServiceProvider.GetRequiredService<AppDbContext>().ExerciseMediaObjectFences.SingleAsync(x => x.ObjectKey == orphan.Key)).IsRetired.Should().BeTrue();
        _storage.BeforeDelete = null;
        using var retry = Scope(); (await retry.ServiceProvider.GetRequiredService<IExerciseMediaService>().CleanupAsync(Actor(), default)).Value.Cleaned.Should().Be(1);
    }

    [Test]
    public async Task UncertainClaimCommit_PerformsNoDeletionUntilFreshConfirmedRetry()
    {
        var orphan = await SeedOrphanAsync(); _uncertainCommit = 1;
        using (var first = Scope()) (await first.ServiceProvider.GetRequiredService<IExerciseMediaService>().CleanupAsync(Actor(), default)).Value.Cleaned.Should().Be(0);
        _storage.DeleteCalls.Should().Be(0);
        using (var check = Scope()) (await check.ServiceProvider.GetRequiredService<AppDbContext>().ExerciseMediaObjectFences.SingleAsync(x => x.ObjectKey == orphan.Key)).IsRetired.Should().BeTrue();
        using var retry = Scope(); (await retry.ServiceProvider.GetRequiredService<IExerciseMediaService>().CleanupAsync(Actor(), default)).Value.Cleaned.Should().Be(1);
    }

    [Test]
    public async Task MoreThanOneBatchOfProtectedHistory_CannotStarveAnUnboundOrphan()
    {
        var orphan = await SeedOrphanAsync();
        using (var seed = Scope())
        {
            var database = seed.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var index = 0; index < 101; index++)
            {
                var exercise = new Exercise { Id = Id<Exercise>.New(), Name = "Retained frontier", BodyPart = BodyParts.Chest };
                var key = $"exercises/{exercise.Id}/{new string('a', 64)}/animation.gif"; var image = _storage.GetPublicUrl(key);
                database.Exercises.Add(exercise);
                database.ExerciseMediaObjectFences.Add(new() { Id = Id<ExerciseMediaObjectFence>.New(), ExerciseId = exercise.Id, ObjectKey = key, PublicUrl = image, CreatedAt = DateTimeOffset.UtcNow.AddDays(-10) });
                database.ExerciseMediaUploads.Add(new() { Id = Id<ExerciseMediaUpload>.New(), ActorId = Id<User>.New(), OperationId = Id<ExerciseMediaOperationReference>.New(), TargetExerciseId = exercise.Id, ObjectKey = key, Status = "bound", ResultImage = image });
            }
            await database.SaveChangesAsync();
        }
        using var cleaner = Scope(); var result = await cleaner.ServiceProvider.GetRequiredService<IExerciseMediaService>().CleanupAsync(Actor(), default);
        result.Value.Cleaned.Should().Be(1); _storage.Deleted.Should().Equal(orphan.Key);
        using var check = Scope(); (await check.ServiceProvider.GetRequiredService<AppDbContext>().ExerciseMediaObjectFences.CountAsync(x => !x.IsRetired)).Should().Be(101);
    }

    [Test]
    public async Task PreviousBindingsAndRemoval_RemainProtectedAndConditionallyRestorable()
    {
        using var seed = Scope(); var database = seed.ServiceProvider.GetRequiredService<AppDbContext>();
        var exercise = new Exercise { Id = Id<Exercise>.New(), Name = "Retained restore fixture", BodyPart = BodyParts.Chest };
        database.Exercises.Add(exercise); await database.SaveChangesAsync(); var actor = Actor(); var input = Replacement();
        using var publisher = Scope();
        var bound = await publisher.ServiceProvider.GetRequiredService<IExerciseMediaService>().ChangeAsync(actor, exercise.Id, input, Gif, default); bound.IsSuccess.Should().BeTrue();
        using (var remove = Scope()) (await remove.ServiceProvider.GetRequiredService<IExerciseMediaService>().ChangeAsync(actor, exercise.Id, new(Id<ExerciseMediaOperationReference>.New(), bound.Value.Image, null, true, null), null, default)).IsSuccess.Should().BeTrue();
        using (var expire = Scope()) { var db = expire.ServiceProvider.GetRequiredService<AppDbContext>(); var row = await db.ExerciseMediaObjectFences.SingleAsync(); row.CreatedAt = DateTimeOffset.UtcNow.AddDays(-10); await db.SaveChangesAsync(); }
        using (var cleanup = Scope()) (await cleanup.ServiceProvider.GetRequiredService<IExerciseMediaService>().CleanupAsync(Actor(), default)).Value.Cleaned.Should().Be(0);
        _storage.DeleteCalls.Should().Be(0);
        using var restore = Scope(); (await restore.ServiceProvider.GetRequiredService<IExerciseMediaService>().ChangeAsync(actor, exercise.Id, new(Id<ExerciseMediaOperationReference>.New(), null, null, false, bound.Value.Image), null, default)).IsSuccess.Should().BeTrue();
    }

    [TestCase("backslash")]
    [TestCase("embedded-tab")]
    [TestCase("authority-slashes")]
    [TestCase("encoded-host")]
    [TestCase("canonical")]
    [TestCase("normalized")]
    [TestCase("http")]
    [TestCase("trailing-dot")]
    public async Task ClaimedOrphan_PersonalLegacyCreatesCannotIntroduceLateReference(string alias)
    {
        var orphan = await SeedOrphanAsync();
        var user = await SeedUserAsync("legacy-media-owner", "legacy-media-owner@example.com");
        var actor = new AuthenticatedAccountContext(user.Id.Rebind<AccountReference>(), null, [], [AuthConstants.Permissions.ManageGlobalExercises], false, false);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _storage.BeforeDelete = async (_, _, cancellationToken) => { entered.TrySetResult(); await release.Task.WaitAsync(cancellationToken); };
        using var cleaner = Scope();
        var cleanup = cleaner.ServiceProvider.GetRequiredService<IExerciseMediaService>().CleanupAsync(Actor(), default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var image = alias switch
            {
                "backslash" => "https:" + new string((char)0x5C, 2) + "exercise.test" + (char)0x5C + orphan.Key.Replace('/', (char)0x5C),
                "embedded-tab" => "https://exer\tcise.test/" + orphan.Key,
                "authority-slashes" => "https:////exercise.test/" + orphan.Key,
                "encoded-host" => "https://%65xercise.test/" + orphan.Key,
                "normalized" => "https://EXERCISE.test:443/old/../" + orphan.Key + "?cache=1#frame",
                "http" => "http://exercise.test/" + orphan.Key,
                "trailing-dot" => "https://exercise.test./" + orphan.Key,
                _ => _storage.GetPublicUrl(orphan.Key)
            };
            using var personal = Scope(); var service = personal.ServiceProvider.GetRequiredService<IExerciseService>();
            var ordinary = await service.AddUserExerciseAsync(actor, new(actor.Id, "Denied personal", BodyParts.Chest, null, image));
            var formula = await service.AddUserExerciseWithFormulaAsync(actor, new(actor.Id, "Denied formula", BodyParts.Chest, ExerciseEloFormula.Standard, null, image));
            ordinary.Error.Should().BeOfType<InvalidExerciseError>(); formula.Error.Should().BeOfType<InvalidExerciseError>();
            using var read = Scope();
            (await read.ServiceProvider.GetRequiredService<AppDbContext>().Exercises.CountAsync(x => x.UserId == user.Id)).Should().Be(0);
            release.TrySetResult(); (await cleanup.WaitAsync(TimeSpan.FromSeconds(30))).Value.Cleaned.Should().Be(1);
            var legacy = "https://legacy.example/" + orphan.Key;
            (await service.AddUserExerciseAsync(actor, new(actor.Id, "Legacy personal", BodyParts.Chest, null, legacy))).IsSuccess.Should().BeTrue();
            (await service.AddUserExerciseWithFormulaAsync(actor, new(actor.Id, "Legacy formula", BodyParts.Chest, ExerciseEloFormula.Standard, null, legacy))).IsSuccess.Should().BeTrue();
        }
        finally { release.TrySetResult(); await cleanup.WaitAsync(TimeSpan.FromSeconds(30)); }
    }

    [Test]
    public async Task PersonalLegacyCreate_CanReferencePermanentlyRetainedPublishedVersion()
    {
        var user = await SeedUserAsync("retained-media-owner", "retained-media-owner@example.com");
        var actor = new AuthenticatedAccountContext(user.Id.Rebind<AccountReference>(), null, [], [AuthConstants.Permissions.ManageGlobalExercises], false, false);
        using var publication = Scope();
        var media = publication.ServiceProvider.GetRequiredService<IExerciseMediaService>();
        var bound = await media.CreateAsync(actor, new(Id<ExerciseMediaOperationReference>.New(), "Retained global", BodyParts.Chest, null, null, Source()), Gif, default);
        bound.IsSuccess.Should().BeTrue();
        Id<Exercise>.TryParse(bound.Value.Id, out var boundId).Should().BeTrue();
        using (var remove = Scope())
            (await remove.ServiceProvider.GetRequiredService<IExerciseMediaService>().ChangeAsync(actor, boundId, new(Id<ExerciseMediaOperationReference>.New(), bound.Value.Image, null, true, null), null, default)).IsSuccess.Should().BeTrue();
        using var personal = Scope(); var service = personal.ServiceProvider.GetRequiredService<IExerciseService>();
        (await service.AddUserExerciseAsync(actor, new(actor.Id, "Retained personal", BodyParts.Chest, null, bound.Value.Image))).IsSuccess.Should().BeTrue();
        (await service.AddUserExerciseWithFormulaAsync(actor, new(actor.Id, "Retained formula", BodyParts.Chest, ExerciseEloFormula.Standard, null, bound.Value.Image))).IsSuccess.Should().BeTrue();
        using var cleanup = Scope(); (await cleanup.ServiceProvider.GetRequiredService<IExerciseMediaService>().CleanupAsync(Actor(), default)).Value.Cleaned.Should().Be(0);
        _storage.DeleteCalls.Should().Be(0);
    }

    [Test]
    public async Task MediaMigration_DownRefusesToDiscardRetiredKeysAndOperationReceipts()
    {
        var orphan = await SeedOrphanAsync();
        using (var cleanup = Scope())
            (await cleanup.ServiceProvider.GetRequiredService<IExerciseMediaService>().CleanupAsync(Actor(), default)).Value.Cleaned.Should().Be(1);
        using var scope = Scope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Func<Task> downgrade = () => db.Database.MigrateAsync("20261108222532_AddTraineeNoteUpdatedAtConcurrency");
        await downgrade.Should().ThrowAsync<InvalidOperationException>().WithMessage("*permanent retired-key fences must be retained*");
        (await db.ExerciseMediaObjectFences.AsNoTracking().SingleAsync(x => x.ObjectKey == orphan.Key)).IsRetired.Should().BeTrue();
        (await db.ExerciseMediaUploads.AsNoTracking().SingleAsync(x => x.OperationId == orphan.Input.OperationId)).Status.Should().Be("retired");
        (await db.Database.GetAppliedMigrationsAsync()).Should().Contain("20261109212642_AddExerciseMediaOperationsAndObjectFences");
    }

    private sealed record Orphan(Exercise Exercise, AuthenticatedAccountContext Actor, ChangeExerciseMediaInput Input, string Key, byte[] Bytes);
    private sealed class SaveGate : IDisposable
    {
        private readonly AppDbContext _database;
        private readonly EventHandler<SavingChangesEventArgs> _handler;
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public SaveGate(AppDbContext database, Func<bool> matches)
        {
            _database = database; var paused = false;
            _handler = (_, _) => { if (!paused && matches()) { paused = true; Arrived.TrySetResult(); _release.Task.GetAwaiter().GetResult(); } };
            database.SavingChanges += _handler;
        }
        public void Release() => _release.TrySetResult();
        public void Dispose() { Release(); _database.SavingChanges -= _handler; }
    }
    private sealed class ControlledStorage : IExerciseMediaStorage
    {
        public ConcurrentDictionary<string, byte[]> Objects { get; } = new();
        public ConcurrentQueue<string> Deleted { get; } = new();
        public bool FailAfterPublish { get; set; }
        public bool FailNextDelete { get; set; }
        public Func<int, string, CancellationToken, Task>? BeforeDelete { get; set; }
        public Func<string, byte[], CancellationToken, Task>? BeforePublish { get; set; }
        public int DeleteCalls; public int PublishCalls;
        public bool IsConfigured => true;
        public string GetPublicUrl(string key) => "https://exercise.test/" + key;
        public Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken) => Task.FromResult(Objects.TryGetValue(key, out var bytes) ? bytes.ToArray() : null);
        public async Task<string> PublishAsync(string key, byte[] bytes, string contentType, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref PublishCalls);
            if (BeforePublish is not null) await BeforePublish(key, bytes, cancellationToken);
            Objects[key] = bytes.ToArray();
            if (FailAfterPublish) { FailAfterPublish = false; throw new IOException("Synthetic uncertain publication"); }
            return GetPublicUrl(key);
        }
        public async Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref DeleteCalls);
            if (BeforeDelete is not null) await BeforeDelete(attempt, key, cancellationToken);
            if (FailNextDelete) { FailNextDelete = false; throw new IOException("Synthetic failed deletion"); }
            Objects.TryRemove(key, out _); Deleted.Enqueue(key);
        }
        public void Reset() { Objects.Clear(); Deleted.Clear(); FailAfterPublish = false; FailNextDelete = false; BeforeDelete = null; BeforePublish = null; DeleteCalls = 0; PublishCalls = 0; }
    }
    private sealed class UncertainCommitUnitOfWork(IUnitOfWork inner, Func<bool> shouldFail) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => inner.SaveChangesAsync(cancellationToken);
        public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => new UncertainTransaction(await inner.BeginTransactionAsync(cancellationToken), shouldFail);
    }
    private sealed class UncertainTransaction(IUnitOfWorkTransaction inner, Func<bool> shouldFail) : IUnitOfWorkTransaction
    {
        public async Task CommitAsync(CancellationToken cancellationToken = default) { await inner.CommitAsync(cancellationToken); if (shouldFail()) throw new IOException("Synthetic lost commit acknowledgement"); }
        public Task RollbackAsync(CancellationToken cancellationToken = default) => inner.RollbackAsync(cancellationToken);
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
