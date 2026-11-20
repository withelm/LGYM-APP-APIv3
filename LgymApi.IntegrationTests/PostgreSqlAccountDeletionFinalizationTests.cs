using LgymApi.Domain.Enums;
using LgymApi.Identity.AccountDeletion.Finalization;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories.AccountDeletion;
using LgymApi.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.IntegrationTests;

[TestFixture]
[Category("PostgreSql")]
internal sealed class PostgreSqlAccountDeletionFinalizationTests : PostgreSqlIntegrationTestBase
{
    [Test]
    public async Task ProcessDueAsync_OneFailedAccountDoesNotPreventAnotherFinalization()
    {
        var first = await SeedUserAsync("failed-finalization", "failed-finalization@example.test");
        var second = await SeedUserAsync("successful-finalization", "successful-finalization@example.test");
        await using (var setup = Factory.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var account in await db.Users.Where(user => user.Id == first.Id || user.Id == second.Id).ToListAsync())
            {
                account.DeletionState = AccountDeletionState.PendingDeletion;
                account.DeletionGeneration = Id<CorrelationScope>.New();
                account.DeletionScheduledGeneration = account.DeletionGeneration;
                account.DeletionEffectiveAt = DateTimeOffset.UtcNow.AddDays(account.Id == first.Id ? -2 : -1);
            }
            await db.SaveChangesAsync();
        }
        await using (var execution = Factory.Services.CreateAsyncScope())
        {
            var db = execution.ServiceProvider.GetRequiredService<AppDbContext>();
            var actual = new AccountFinalizationCleanup(db);
            var cleanup = Substitute.For<IAccountFinalizationCleanup>();
            cleanup.StageAsync(Arg.Any<AccountFinalizationCandidate>(), Arg.Any<CancellationToken>()).Returns(call =>
                call.Arg<AccountFinalizationCandidate>().AccountId.Value == first.Id.Value
                    ? Task.FromException(new InvalidOperationException("one account failure"))
                    : actual.StageAsync(call.Arg<AccountFinalizationCandidate>(), call.Arg<CancellationToken>()));
            cleanup.StageRelationshipHistoryAsync(Arg.Any<AccountFinalizationCandidate>(), Arg.Any<CancellationToken>())
                .Returns(call => actual.StageRelationshipHistoryAsync(call.Arg<AccountFinalizationCandidate>(), call.Arg<CancellationToken>()));
            var service = new AccountFinalizationService(new AccountFinalizationStore(db),
                execution.ServiceProvider.GetRequiredService<IAccountFinalizationPhotoManifest>(), cleanup, new EfUnitOfWork(db), TimeProvider.System);

            await Assert.ThatAsync(async () => { await (((Func<Task>)(() => service.ProcessDueAsync())))(); }, Throws.InstanceOf<InvalidOperationException>());
        }
        await using var verify = Factory.Services.CreateAsyncScope();
        var database = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That((await database.Users.IgnoreQueryFilters().SingleAsync(user => user.Id == first.Id)).DeletionState, Is.EqualTo(AccountDeletionState.PendingDeletion));
        Assert.That((await database.Users.IgnoreQueryFilters().SingleAsync(user => user.Id == second.Id)).DeletionState, Is.EqualTo(AccountDeletionState.PermanentlyDeleted));
    }

    [Test]
    public async Task ProcessDueAsync_RollsBackSetBasedCleanupWhenCompletionFails()
    {
        var user = await SeedUserAsync();
        var measurementId = Id<Measurement>.New();
        await using (var setup = Factory.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            var account = await db.Users.SingleAsync(x => x.Id == user.Id);
            account.DeletionState = AccountDeletionState.PendingDeletion;
            account.DeletionGeneration = Id<CorrelationScope>.New();
            account.DeletionScheduledGeneration = account.DeletionGeneration;
            account.DeletionEffectiveAt = DateTimeOffset.UtcNow.AddDays(-1);
            db.Measurements.Add(new Measurement { Id = measurementId, UserId = user.Id, Value = 42, Unit = "cm" });
            await db.SaveChangesAsync();
        }
        await using (var execution = Factory.Services.CreateAsyncScope())
        {
            var db = execution.ServiceProvider.GetRequiredService<AppDbContext>();
            var actual = new AccountFinalizationStore(db);
            var store = Substitute.For<IAccountFinalizationStore>();
            store.FindDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(call => actual.FindDueAsync(call.Arg<DateTimeOffset>(), call.Arg<int>(), call.Arg<CancellationToken>()));
            store.LockAndBeginAsync(Arg.Any<AccountFinalizationCandidate>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
                .Returns(call => actual.LockAndBeginAsync(call.Arg<AccountFinalizationCandidate>(), call.Arg<DateTimeOffset>(), call.Arg<CancellationToken>()));
            store.StageCompletedAsync(Arg.Any<AccountFinalizationCandidate>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException(new InvalidOperationException("completion failure")));
            var service = new AccountFinalizationService(store, Substitute.For<IAccountFinalizationPhotoManifest>(),
                new AccountFinalizationCleanup(db), new EfUnitOfWork(db), TimeProvider.System);

            await Assert.ThatAsync(async () => { await (((Func<Task>)(() => service.ProcessDueAsync())))(); }, Throws.InstanceOf<InvalidOperationException>());
        }
        await using var verify = Factory.Services.CreateAsyncScope();
        var database = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That((await database.Measurements.AnyAsync(x => x.Id == measurementId)), Is.True);
        Assert.That((await database.Users.SingleAsync(x => x.Id == user.Id)).DeletionState, Is.EqualTo(AccountDeletionState.PendingDeletion));
    }

    [Test]
    public async Task ProcessDueAsync_DeletesAccountHistoryAndPersistsPhotoCleanupBeforeLosingReferences()
    {
        var user = await SeedUserAsync();
        var trainer = await SeedUserAsync("finalization-trainer", "finalization-trainer@example.test");
        var generation = Id<CorrelationScope>.New();
        var key = $"photos/{user.Id}/original.jpg";
        var thumbnail = $"photos/{user.Id}/thumbnail.jpg";
        await using (var setup = Factory.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            var account = await db.Users.SingleAsync(x => x.Id == user.Id);
            account.DeletionState = AccountDeletionState.PendingDeletion;
            account.DeletionGeneration = generation;
            account.DeletionScheduledGeneration = generation;
            account.DeletionRequestedAt = DateTimeOffset.UtcNow.AddDays(-31);
            account.DeletionEffectiveAt = DateTimeOffset.UtcNow.AddDays(-1);
            var template = new ReportTemplate { Id = Id<ReportTemplate>.New(), TrainerId = trainer.Id, Name = "Report" };
            var request = new ReportRequest
            {
                Id = Id<ReportRequest>.New(), TrainerId = trainer.Id, TraineeId = user.Id,
                TemplateId = template.Id, Status = ReportRequestStatus.Submitted
            };
            db.ReportTemplates.Add(template);
            db.ReportRequests.Add(request);
            db.ReportSubmissions.Add(new ReportSubmission
            {
                Id = Id<ReportSubmission>.New(), ReportRequestId = request.Id, TraineeId = user.Id,
                PayloadJson = "{\"private\":\"answer\"}"
            });
            db.Photos.Add(new Photo
            {
                Id = Id<Photo>.New(), ReportRequestId = request.Id, OwnerUserId = user.Id, UploaderUserId = trainer.Id,
                StorageKey = key, ThumbnailStorageKey = thumbnail, IsDeleted = true
            });
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(), TrainerId = trainer.Id, TraineeId = user.Id
            });
            db.TraineeNotes.Add(new TraineeNote
            {
                Id = Id<TraineeNote>.New(), TrainerId = trainer.Id, TraineeId = user.Id,
                LastUpdatedByUserId = trainer.Id, Content = "private note"
            });
            db.Measurements.Add(new Measurement { Id = Id<Measurement>.New(), UserId = user.Id, Value = 42, Unit = "cm" });
            var address = new Address { Id = Id<Address>.New(), City = "private city" };
            db.Addresses.Add(address);
            db.Gyms.Add(new Gym { Id = Id<Gym>.New(), UserId = user.Id, Name = "private gym", AddressId = address.Id });
            await db.SaveChangesAsync();
        }

        await using (var execution = Factory.Services.CreateAsyncScope())
        {
            await execution.ServiceProvider.GetRequiredService<IAccountFinalizationService>().ProcessDueAsync();
        }
        await using (var replay = Factory.Services.CreateAsyncScope())
        {
            await replay.ServiceProvider.GetRequiredService<IAccountFinalizationService>().ProcessDueAsync();
        }

        await using var verify = Factory.Services.CreateAsyncScope();
        var database = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var deleted = await database.Users.IgnoreQueryFilters().SingleAsync(x => x.Id == user.Id);
        Assert.That(deleted.DeletionState, Is.EqualTo(AccountDeletionState.PermanentlyDeleted));
        Assert.That(deleted.LegacyHash, Is.Null);
        var assertedChain12 = deleted.Name;
        Assert.That(assertedChain12, Does.Not.Contain(user.Id.ToString()));
        Assert.That(assertedChain12, Is.Not.EqualTo(user.Name));
        Assert.That(deleted.Email, Is.Not.EqualTo(user.Email));
        Assert.That((await database.Users.AnyAsync(x => x.Id == trainer.Id)), Is.True);
        Assert.That((await database.ReportRequests.IgnoreQueryFilters().CountAsync()), Is.EqualTo(0));
        Assert.That((await database.ReportSubmissions.IgnoreQueryFilters().CountAsync()), Is.EqualTo(0));
        Assert.That((await database.Photos.IgnoreQueryFilters().CountAsync()), Is.EqualTo(0));
        Assert.That((await database.CoachingEpisodes.IgnoreQueryFilters().CountAsync()), Is.EqualTo(0));
        Assert.That((await database.TraineeNotes.IgnoreQueryFilters().CountAsync()), Is.EqualTo(0));
        Assert.That((await database.Measurements.IgnoreQueryFilters().CountAsync()), Is.EqualTo(0));
        Assert.That((await database.Addresses.IgnoreQueryFilters().CountAsync()), Is.EqualTo(0));
        var work = await database.PhotoDeletionWorkItems.Where(x => x.DeletionGeneration == generation && !x.IsPrefix).ToListAsync();
        Assert.That(work.Select(x => x.StorageKey), Is.EquivalentTo(new[] { key, thumbnail }));
        Assert.That((work).All(x => x.CompletedAtUtc == null), Is.True);
    }

    [Test]
    public async Task LockAndBeginAsync_RejectsCandidateCancelledAfterScan()
    {
        var user = await SeedUserAsync();
        var now = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
        await using (var setup = Factory.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            var account = await db.Users.SingleAsync(x => x.Id == user.Id);
            account.DeletionState = AccountDeletionState.PendingDeletion;
            account.DeletionEffectiveAt = now;
            account.DeletionGeneration = Id<CorrelationScope>.New();
            account.DeletionScheduledGeneration = account.DeletionGeneration;
            await db.SaveChangesAsync();
        }

        await using var scan = Factory.Services.CreateAsyncScope();
        var scanDb = scan.ServiceProvider.GetRequiredService<AppDbContext>();
        var store = new AccountFinalizationStore(scanDb);
        var candidate = (await store.FindDueAsync(now, 100)).Single(x => x.AccountId.Value == user.Id.Value);
        await using (var cancel = Factory.Services.CreateAsyncScope())
        {
            var db = cancel.ServiceProvider.GetRequiredService<AppDbContext>();
            var account = await db.Users.SingleAsync(x => x.Id == user.Id);
            account.DeletionState = AccountDeletionState.Active;
            account.DeletionGeneration = null;
            account.DeletionScheduledGeneration = null;
            account.LifecycleRevision = Id<CorrelationScope>.New();
            await db.SaveChangesAsync();
        }

        await using var transaction = await new EfUnitOfWork(scanDb).BeginTransactionAsync();
        Assert.That((await store.LockAndBeginAsync(candidate, now)), Is.False);
    }

    [Test]
    public async Task ProcessDueAsync_LeavesAccountPendingWhenManifestStagingFails()
    {
        var user = await SeedUserAsync();
        var generation = Id<CorrelationScope>.New();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var account = await db.Users.SingleAsync(x => x.Id == user.Id);
            account.DeletionState = AccountDeletionState.PendingDeletion;
            account.DeletionEffectiveAt = DateTimeOffset.UtcNow.AddDays(-1);
            account.DeletionGeneration = generation;
            account.DeletionScheduledGeneration = generation;
            await db.SaveChangesAsync();
        }

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var manifest = Substitute.For<IAccountFinalizationPhotoManifest>();
            manifest.StageAsync(Arg.Any<AccountFinalizationCandidate>(), Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromException(new InvalidOperationException("manifest failure")));
            var service = new AccountFinalizationService(new AccountFinalizationStore(db), manifest,
                new AccountFinalizationCleanup(db), new EfUnitOfWork(db), TimeProvider.System);
            await Assert.ThatAsync(async () => { await service.ProcessDueAsync(); }, Throws.InstanceOf<InvalidOperationException>());
        }

        await using var verify = Factory.Services.CreateAsyncScope();
        var persisted = await verify.ServiceProvider.GetRequiredService<AppDbContext>().Users.IgnoreQueryFilters().SingleAsync(x => x.Id == user.Id);
        Assert.That(persisted.DeletionState, Is.EqualTo(AccountDeletionState.PendingDeletion));
        Assert.That(persisted.DeletionGeneration, Is.EqualTo(generation));
        Assert.That(persisted.Email, Is.EqualTo(user.Email));
    }

    [Test]
    public async Task ProcessDueAsync_DoesNotFinalizeBeforeDeadline()
    {
        var user = await SeedUserAsync();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var account = await db.Users.SingleAsync(x => x.Id == user.Id);
            account.DeletionState = AccountDeletionState.PendingDeletion;
            account.DeletionEffectiveAt = DateTimeOffset.UtcNow.AddDays(1);
            account.DeletionGeneration = Id<CorrelationScope>.New();
            account.DeletionScheduledGeneration = account.DeletionGeneration;
            await db.SaveChangesAsync();
            var service = new AccountFinalizationService(new AccountFinalizationStore(db),
                Substitute.For<IAccountFinalizationPhotoManifest>(), new AccountFinalizationCleanup(db),
                new EfUnitOfWork(db), TimeProvider.System);
            await service.ProcessDueAsync();
        }

        await using var verify = Factory.Services.CreateAsyncScope();
        var persisted = await verify.ServiceProvider.GetRequiredService<AppDbContext>().Users.IgnoreQueryFilters().SingleAsync(x => x.Id == user.Id);
        Assert.That(persisted.DeletionState, Is.EqualTo(AccountDeletionState.PendingDeletion));
        Assert.That(persisted.Email, Is.EqualTo(user.Email));
    }
}
