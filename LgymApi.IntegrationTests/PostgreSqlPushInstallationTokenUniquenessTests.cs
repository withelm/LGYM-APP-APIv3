using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Notifications.Repositories;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
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
            baseline.PushInstallations.AddRange(
                CreateInstallation("duplicate-device-a", "duplicate-token"),
                CreateInstallation("duplicate-device-b", "duplicate-token"),
                CreateInstallation("unrelated-device", "unrelated-token"));
            await baseline.SaveChangesAsync();
        }

        await using (var migration = CreateDbContext(lease))
        {
            var migrations = migration.Database.GetMigrations().ToList();
            var targetMigration = migrations.Single(candidate => candidate.EndsWith(MigrationName, StringComparison.Ordinal));
            migrations.IndexOf(PreviousMigrationId).Should().Be(migrations.IndexOf(targetMigration) - 1);
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

        conflicts.Should().HaveCount(2);
        conflicts.Should().OnlyContain(installation =>
            installation.FcmToken == null
            && installation.DisabledAt != null
            && installation.DisabledReason == "DuplicateFcmTokenMigration"
            && installation.IsDeleted
            && installation.UserId == null
            && installation.SessionId == null);
        unrelated.FcmToken.Should().Be("unrelated-token");
        (await IsUniqueFilteredTokenIndexAsync(lease.ConnectionString)).Should().BeTrue();
        verification.Database.HasPendingModelChanges().Should().BeFalse();
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

        results.Should().ContainSingle(result => result.IsSuccess);
        results.Should().ContainSingle(result => result.IsFailure && result.Error is ForbiddenError);
        await using var assertionScope = Factory.Services.CreateAsyncScope();
        var database = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await database.PushInstallations.AsNoTracking()
            .Where(installation => installation.FcmToken == "shared-race-token")
            .ToListAsync();
        persisted.Should().ContainSingle();
        persisted[0].InstallationId.Should().BeOneOf("race-device-a", "race-device-b");
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

        results.Should().ContainSingle(result => result.IsSuccess);
        results.Should().ContainSingle(result => result.IsFailure && result.Error is ForbiddenError);
        await using var assertionScope = Factory.Services.CreateAsyncScope();
        var persisted = await assertionScope.ServiceProvider.GetRequiredService<AppDbContext>()
            .PushInstallations
            .AsNoTracking()
            .SingleAsync(installation => installation.InstallationId == "shared-disassociated-device");
        persisted.UserId.Should().BeOneOf(firstUser.Id, secondUser.Id);
        persisted.SessionId.Should().BeOneOf(firstSession.Id, secondSession.Id);
        persisted.FcmToken.Should().BeOneOf("claim-token-a", "claim-token-b");
        persisted.RegistrationRevision.Should().Be(1);
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

        responses.Select(response => response.StatusCode)
            .Should()
            .BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Forbidden]);
        await using var assertionScope = Factory.Services.CreateAsyncScope();
        var database = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await database.PushInstallations.AsNoTracking()
            .CountAsync(installation => installation.FcmToken == "push-http-shared-token"))
            .Should()
            .Be(1);
        (await database.ApiIdempotencyRecords.AsNoTracking()
            .Where(record => record.IdempotencyKey == "push-http-race-a" || record.IdempotencyKey == "push-http-race-b")
            .Select(record => record.ResponseStatusCode)
            .ToListAsync())
            .Should()
            .BeEquivalentTo([(int)HttpStatusCode.OK, (int)HttpStatusCode.Forbidden]);
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = document.RootElement.GetProperty("token").GetString();
        token.Should().NotBeNullOrWhiteSpace();
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
}
