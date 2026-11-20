using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Contracts.Push;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Notifications.Repositories;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using LgymApi.Infrastructure.UnitOfWork;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("PostgreSql")]
public sealed class PostgreSqlPushInstallationTokenUniquenessTests : PostgreSqlIntegrationTestBase
{
    private const string PreviousMigrationId = "20261107094438_AddCoachingEpisodes";
    private const string MigrationName = "EnforceUniquePushInstallationTokens";
    private const string TokenIndexName = "IX_PushInstallations_FcmToken";

    [Test]
    public async Task Migration_WhenDuplicateTokensExist_TombstonesEveryConflictWithoutChoosingAWinner()
    {
        await using var lease = await PostgreSqlDatabaseLease.CreateAsync();
        await using (var baseline = CreateDbContext(lease))
        {
            await MigrateAsync(baseline, PreviousMigrationId);
            // Seed the historical schema without the current model's RegistrationRevision column.
            await using var connection = new NpgsqlConnection(lease.ConnectionString);
            await connection.OpenAsync();
            foreach (var installation in new[]
            {
                CreateInstallation("duplicate-device-a", "duplicate-token"),
                CreateInstallation("duplicate-device-b", "duplicate-token"),
                CreateInstallation("unrelated-device", "unrelated-token")
            })
            {
                await using var command = new NpgsqlCommand("""
                    INSERT INTO "PushInstallations"
                        ("Id", "InstallationId", "Platform", "FcmToken", "Environment", "LastSeenAt", "CreatedAt", "UpdatedAt", "IsDeleted")
                    VALUES (CAST(@id AS uuid), @installationId, @platform, @token, @environment, @now, @now, @now, FALSE)
                    """, connection);
                command.Parameters.AddWithValue("id", installation.Id.ToString());
                command.Parameters.AddWithValue("installationId", installation.InstallationId);
                command.Parameters.AddWithValue("platform", installation.Platform);
                command.Parameters.AddWithValue("token", installation.FcmToken);
                command.Parameters.AddWithValue("environment", installation.Environment);
                command.Parameters.AddWithValue("now", installation.LastSeenAt);
                await command.ExecuteNonQueryAsync();
            }
        }

        await using (var migration = CreateDbContext(lease))
        {
            var migrations = migration.Database.GetMigrations().ToList();
            var targetMigration = migrations.Single(candidate => candidate.EndsWith(MigrationName, StringComparison.Ordinal));
            Assert.That(migrations.IndexOf(PreviousMigrationId), Is.EqualTo(migrations.IndexOf(targetMigration) - 1));
            await MigrateAsync(migration, targetMigration);
        }

        await using var verification = CreateDbContext(lease);
        var conflicts = await verification.PushInstallations
            .IgnoreQueryFilters()
            .Where(installation => installation.InstallationId.StartsWith("duplicate-device"))
            .OrderBy(installation => installation.InstallationId)
            .ToListAsync();
        var unrelated = await verification.PushInstallations
            .SingleAsync(installation => installation.InstallationId == "unrelated-device");

        Assert.That(conflicts, Has.Exactly(2).Items);
        Assert.That((conflicts).All(installation =>
            installation.FcmToken == null
            && installation.DisabledAt != null
            && installation.DisabledReason == "DuplicateFcmTokenMigration"
            && installation.IsDeleted
            && installation.UserId == null
            && installation.SessionId == null), Is.True);
        Assert.That(unrelated.FcmToken, Is.EqualTo("unrelated-token"));
        Assert.That((await IsUniqueFilteredTokenIndexAsync(lease.ConnectionString)), Is.True);
        Assert.That(verification.Database.HasPendingModelChanges(), Is.False);
    }

    [Test]
    public async Task RegisterAsync_ConcurrentDifferentAccountsWithSameToken_OneSucceedsAndOneFailsClosed()
    {
        var firstIdentity = Id<User>.New();
        var secondIdentity = Id<User>.New();
        var firstUser = await SeedUserAsync($"push-race-a-{firstIdentity}", $"push-race-a-{firstIdentity}@example.com");
        var secondUser = await SeedUserAsync($"push-race-b-{secondIdentity}", $"push-race-b-{secondIdentity}@example.com");
        var firstSession = await SeedSessionAsync(firstUser.Id);
        var secondSession = await SeedSessionAsync(secondUser.Id);
        var barrier = new SaveBarrier();
        await using var firstScope = Factory.Services.CreateAsyncScope();
        await using var secondScope = Factory.Services.CreateAsyncScope();
        var firstService = CreateLifecycleService(firstScope.ServiceProvider, barrier);
        var secondService = CreateLifecycleService(secondScope.ServiceProvider, barrier);

        var results = await Task.WhenAll(
            firstService.RegisterAsync(
                firstUser.Id,
                firstSession.Id,
                new RegisterPushInstallationInput("race-device-a", "android", "shared-race-token", null, "testing", null)),
            secondService.RegisterAsync(
                secondUser.Id,
                secondSession.Id,
                new RegisterPushInstallationInput("race-device-b", "ios", "shared-race-token", null, "testing", null)));

        Assert.That((results).Where(result => result.IsSuccess), Has.Exactly(1).Items);
        Assert.That((results).Where(result => result.IsFailure && result.Error is ForbiddenError), Has.Exactly(1).Items);
        await using var assertionScope = Factory.Services.CreateAsyncScope();
        var database = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await database.PushInstallations.AsNoTracking()
            .Where(installation => installation.FcmToken == "shared-race-token")
            .ToListAsync();
        Assert.That(persisted, Has.Exactly(1).Items);
        Assert.That(persisted[0].InstallationId, Is.AnyOf("race-device-a", "race-device-b"));
    }

    [Test]
    public async Task RegisterAsync_ConcurrentClaimsOfSameDisassociatedInstallation_OneSucceedsAndOneFailsClosed()
    {
        var firstIdentity = Id<User>.New();
        var secondIdentity = Id<User>.New();
        var firstUser = await SeedUserAsync($"push-claim-a-{firstIdentity}", $"push-claim-a-{firstIdentity}@example.com");
        var secondUser = await SeedUserAsync($"push-claim-b-{secondIdentity}", $"push-claim-b-{secondIdentity}@example.com");
        var firstSession = await SeedSessionAsync(firstUser.Id);
        var secondSession = await SeedSessionAsync(secondUser.Id);
        await using (var seedScope = Factory.Services.CreateAsyncScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.PushInstallations.Add(CreateInstallation("shared-disassociated-device", "unbound-token"));
            await database.SaveChangesAsync();
        }

        var barrier = new SaveBarrier();
        await using var firstScope = Factory.Services.CreateAsyncScope();
        await using var secondScope = Factory.Services.CreateAsyncScope();
        var firstService = CreateLifecycleService(firstScope.ServiceProvider, barrier);
        var secondService = CreateLifecycleService(secondScope.ServiceProvider, barrier);

        var results = await Task.WhenAll(
            firstService.RegisterAsync(
                firstUser.Id,
                firstSession.Id,
                new RegisterPushInstallationInput("shared-disassociated-device", "android", "claim-token-a", null, "testing", null)),
            secondService.RegisterAsync(
                secondUser.Id,
                secondSession.Id,
                new RegisterPushInstallationInput("shared-disassociated-device", "ios", "claim-token-b", null, "testing", null)));

        Assert.That((results).Where(result => result.IsSuccess), Has.Exactly(1).Items);
        Assert.That((results).Where(result => result.IsFailure && result.Error is ForbiddenError), Has.Exactly(1).Items);
        await using var assertionScope = Factory.Services.CreateAsyncScope();
        var persisted = await assertionScope.ServiceProvider.GetRequiredService<AppDbContext>()
            .PushInstallations
            .AsNoTracking()
            .SingleAsync(installation => installation.InstallationId == "shared-disassociated-device");
        Assert.That(persisted.UserId, Is.AnyOf(firstUser.Id, secondUser.Id));
        Assert.That(persisted.SessionId, Is.AnyOf(firstSession.Id, secondSession.Id));
        Assert.That(persisted.FcmToken, Is.AnyOf("claim-token-a", "claim-token-b"));
        Assert.That(persisted.RegistrationRevision, Is.EqualTo(1));
    }

    [Test]
    public async Task RegisterEndpoint_KeyedConcurrentTokenRace_ReturnsOkAndForbiddenWithoutRetryingFailedInsert()
    {
        var firstIdentity = Id<User>.New();
        var secondIdentity = Id<User>.New();
        var firstUser = await SeedUserAsync($"push-http-a-{firstIdentity}", $"push-http-a-{firstIdentity}@example.com");
        var secondUser = await SeedUserAsync($"push-http-b-{secondIdentity}", $"push-http-b-{secondIdentity}@example.com");
        var barrier = new SaveBarrier();
        using var raceFactory = Factory.WithWebHostBuilder(builder => ConfigureRegistrationBarrier(builder, barrier));
        using var firstClient = raceFactory.CreateClient();
        using var secondClient = raceFactory.CreateClient();
        await AuthenticateAsync(firstClient, firstUser);
        await AuthenticateAsync(secondClient, secondUser);
        firstClient.DefaultRequestHeaders.Add("Idempotency-Key", "push-http-race-a");
        secondClient.DefaultRequestHeaders.Add("Idempotency-Key", "push-http-race-b");

        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync("/api/push/installations/register", new
            {
                installationId = "push-http-device-a",
                platform = "android",
                fcmToken = "push-http-shared-token",
                environment = "testing"
            }),
            secondClient.PostAsJsonAsync("/api/push/installations/register", new
            {
                installationId = "push-http-device-b",
                platform = "ios",
                fcmToken = "push-http-shared-token",
                environment = "testing"
            }));

        Assert.That(responses.Select(response => response.StatusCode), Is.EquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Forbidden }));
        await using var assertionScope = Factory.Services.CreateAsyncScope();
        var database = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That((await database.PushInstallations.AsNoTracking()
            .CountAsync(installation => installation.FcmToken == "push-http-shared-token")), Is.EqualTo(1));
        Assert.That((await database.ApiIdempotencyRecords.AsNoTracking()
            .Where(record => record.IdempotencyKey == "push-http-race-a" || record.IdempotencyKey == "push-http-race-b")
            .Select(record => record.ResponseStatusCode)
            .ToListAsync()), Is.EquivalentTo(new[] { (int)HttpStatusCode.OK, (int)HttpStatusCode.Forbidden }));
    }

    [Test]
    public async Task ProcessAsync_WhenRegistrationRotatesDuringInvalidTokenDelivery_PreservesTheReplacementAndRecoversWithCanceledRequestToken()
    {
        var user = await SeedUserAsync($"push-delivery-{Id<User>.New():N}", $"push-delivery-{Id<User>.New():N}@example.com");
        var originalSession = await SeedSessionAsync(user.Id);
        var replacementSession = await SeedSessionAsync(user.Id);
        var installation = CreateInstallation("delivery-rotation-device", "token-a");
        installation.UserId = user.Id;
        installation.SessionId = originalSession.Id;
        var message = new PushNotificationMessage
        {
            Id = Id<PushNotificationMessage>.New(),
            UserId = user.Id,
            PushInstallationId = installation.Id,
            SchemaVersion = 1,
            Type = "trainer.note.updated",
            EventId = "delivery-rotation-event",
            PayloadJson = """{"schemaVersion":1,"type":"trainer.note.updated","eventId":"delivery-rotation-event"}""",
            Status = PushNotificationStatus.Pending
        };
        await using (var seedScope = Factory.Services.CreateAsyncScope())
        {
            var database = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.PushInstallations.Add(installation);
            database.PushNotificationMessages.Add(message);
            await database.SaveChangesAsync();
        }

        using var cancellationTokenSource = new CancellationTokenSource();
        await using var deliveryScope = Factory.Services.CreateAsyncScope();
        var deliveryDatabase = deliveryScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = new RotatingInvalidTokenSender();
        var scheduler = new CapturingPushScheduler();
        var unitOfWork = new RecordingCancellationUnitOfWork(new EfUnitOfWork(deliveryDatabase));
        var deliveryService = new PushNotificationDeliveryService(
            new PushNotificationMessageRepository(deliveryDatabase),
            deliveryScope.ServiceProvider.GetRequiredService<INotificationDisclosureEligibilityService>(),
            new PushInstallationRepository(deliveryDatabase),
            sender,
            scheduler,
            new SingleRetrySettings(),
            unitOfWork,
            deliveryScope.ServiceProvider.GetRequiredService<IAuthenticatedAccountContextResolver>(),
            NullLogger<PushNotificationDeliveryService>.Instance);

        var deliveryTask = deliveryService.ProcessAsync(message.Id.ToString(), cancellationTokenSource.Token);
        await sender.WaitUntilDeliveryStartsAsync();

        await using (var registrationScope = Factory.Services.CreateAsyncScope())
        {
            var registrationService = new PushInstallationLifecycleService(
                new PushInstallationRepository(registrationScope.ServiceProvider.GetRequiredService<AppDbContext>()),
                registrationScope.ServiceProvider.GetRequiredService<IPushInstallationPersistenceConflictClassifier>(),
                new EfUnitOfWork(registrationScope.ServiceProvider.GetRequiredService<AppDbContext>()));
            var registration = await registrationService.RegisterAsync(
                user.Id,
                replacementSession.Id,
                new RegisterPushInstallationInput("delivery-rotation-device", "android", "token-b", null, "testing", "authorized"));

            Assert.That(registration.IsSuccess, Is.True);
        }

        cancellationTokenSource.Cancel();
        sender.ReturnInvalidToken();

        Func<Task> awaitDelivery = async () => await deliveryTask;
        await Assert.ThatAsync(async () => { await awaitDelivery(); }, Throws.InstanceOf<DbUpdateConcurrencyException>());

        Assert.That(sender.TokenObservedDuringDelivery, Is.EqualTo("token-a"));
        Assert.That(scheduler.ScheduleRetryCalls, Is.EqualTo(1));
        Assert.That(unitOfWork.CancellationTokens, Has.Exactly(3).Items);
        Assert.That(unitOfWork.CancellationTokens[0].IsCancellationRequested, Is.True);
        Assert.That((unitOfWork.CancellationTokens.Skip(1)).All(token => !token.CanBeCanceled), Is.True);

        await using var assertionScope = Factory.Services.CreateAsyncScope();
        var assertionDatabase = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedInstallation = await assertionDatabase.PushInstallations.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == installation.Id);
        var persistedMessage = await assertionDatabase.PushNotificationMessages.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == message.Id);

        Assert.That(persistedInstallation.FcmToken, Is.EqualTo("token-b"));
        Assert.That(persistedInstallation.SessionId, Is.EqualTo(replacementSession.Id));
        Assert.That(persistedInstallation.RegistrationRevision, Is.EqualTo(1));
        Assert.That(persistedInstallation.DisabledAt, Is.Null);
        Assert.That(persistedInstallation.DisabledReason, Is.Null);
        Assert.That(persistedMessage.Status, Is.EqualTo(PushNotificationStatus.Failed));
        Assert.That(persistedMessage.FailureKind, Is.EqualTo(PushNotificationFailureKind.Transient));
        Assert.That(persistedMessage.Attempts, Is.EqualTo(1));
        Assert.That(persistedMessage.SchedulerJobId, Is.EqualTo("delivery-rotation-retry"));
        Assert.That(persistedMessage.NextAttemptAt, Is.Not.Null);
    }

    private async Task<UserSession> SeedSessionAsync(Id<User> userId)
    {
        var session = new UserSession
        {
            Id = Id<UserSession>.New(),
            UserId = userId,
            Jti = Id<UserSession>.New().ToString(),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
        };
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        database.UserSessions.Add(session);
        await database.SaveChangesAsync();
        return session;
    }

    private static void ConfigureRegistrationBarrier(IWebHostBuilder builder, SaveBarrier barrier)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork>(provider =>
            {
                var database = provider.GetRequiredService<AppDbContext>();
                return new RegistrationBarrierUnitOfWork(database, new EfUnitOfWork(database), barrier);
            });
        });
    }

    private static async Task AuthenticateAsync(HttpClient client, User user)
    {
        var response = await client.PostAsJsonAsync("/api/login", new
        {
            name = user.Name,
            password = "password123"
        });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = document.RootElement.GetProperty("token").GetString();
        Assert.That(string.IsNullOrWhiteSpace(token), Is.False);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static PushInstallationLifecycleService CreateLifecycleService(
        IServiceProvider services,
        SaveBarrier barrier)
    {
        var database = services.GetRequiredService<AppDbContext>();
        return new PushInstallationLifecycleService(
            new PushInstallationRepository(database),
            services.GetRequiredService<IPushInstallationPersistenceConflictClassifier>(),
            new BarrierUnitOfWork(new EfUnitOfWork(database), barrier));
    }

    private static PushInstallation CreateInstallation(string installationId, string token) => new()
    {
        Id = Id<PushInstallation>.New(),
        InstallationId = installationId,
        Platform = "android",
        FcmToken = token,
        Environment = "testing",
        LastSeenAt = DateTimeOffset.UtcNow
    };

    private static AppDbContext CreateDbContext(PostgreSqlDatabaseLease lease)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(lease.ConnectionString).Options);

    private static Task MigrateAsync(AppDbContext database, string targetMigration)
        => database.Database.GetService<IMigrator>().MigrateAsync(targetMigration);

    private static async Task<bool> IsUniqueFilteredTokenIndexAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT indexdef
            FROM pg_indexes
            WHERE schemaname = 'public' AND indexname = @indexName
            """,
            connection);
        command.Parameters.AddWithValue("indexName", TokenIndexName);
        var definition = (string?)await command.ExecuteScalarAsync();
        return definition?.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true
            && definition.Contains("FcmToken", StringComparison.Ordinal)
            && definition.Contains("IS NOT NULL", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class SaveBarrier
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrivals) == 2)
            {
                _release.TrySetResult();
            }

            await _release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }

    private sealed class BarrierUnitOfWork(IUnitOfWork inner, SaveBarrier barrier) : IUnitOfWork
    {
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await barrier.WaitAsync(cancellationToken);
            return await inner.SaveChangesAsync(cancellationToken);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => inner.BeginTransactionAsync(cancellationToken);
    }

    private sealed class RegistrationBarrierUnitOfWork(
        AppDbContext database,
        IUnitOfWork inner,
        SaveBarrier barrier) : IUnitOfWork
    {
        private bool _registrationSaveObserved;

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_registrationSaveObserved
                && database.ChangeTracker.Entries<PushInstallation>()
                    .Any(entry => entry.State is EntityState.Added or EntityState.Modified))
            {
                _registrationSaveObserved = true;
                await barrier.WaitAsync(cancellationToken);
            }

            return await inner.SaveChangesAsync(cancellationToken);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => inner.BeginTransactionAsync(cancellationToken);
    }

    private sealed class RotatingInvalidTokenSender : IPushProviderSender
    {
        private readonly TaskCompletionSource _deliveryStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _returnInvalidToken = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? TokenObservedDuringDelivery { get; private set; }

        public async Task<PushSendAttemptResult> SendAsync(
            PushInstallationDeliveryTarget target,
            PushEventPayload payload,
            CancellationToken cancellationToken = default)
        {
            TokenObservedDuringDelivery = target.FcmToken;
            _deliveryStarted.TrySetResult();
            await _returnInvalidToken.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return new PushSendAttemptResult(PushSendOutcome.InvalidToken, "BadRequest", null, "UNREGISTERED", "unregistered");
        }

        public Task WaitUntilDeliveryStartsAsync()
            => _deliveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public void ReturnInvalidToken() => _returnInvalidToken.TrySetResult();
    }

    private sealed class CapturingPushScheduler : IPushBackgroundScheduler
    {
        public int ScheduleRetryCalls { get; private set; }

        public string? Enqueue(string notificationId) => throw new InvalidOperationException("Unexpected initial enqueue.");

        public string? ScheduleRetry(string notificationId, TimeSpan delay)
        {
            ScheduleRetryCalls += 1;
            return "delivery-rotation-retry";
        }
    }

    private sealed class SingleRetrySettings : IPushNotificationDeliveryRetrySettings
    {
        public IReadOnlyList<int> RetryDelaysSeconds { get; } = [1];
    }

    private sealed class RecordingCancellationUnitOfWork(IUnitOfWork inner) : IUnitOfWork
    {
        public List<CancellationToken> CancellationTokens { get; } = [];

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            CancellationTokens.Add(cancellationToken);
            return inner.SaveChangesAsync(CancellationToken.None);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => inner.BeginTransactionAsync(cancellationToken);
    }
}
