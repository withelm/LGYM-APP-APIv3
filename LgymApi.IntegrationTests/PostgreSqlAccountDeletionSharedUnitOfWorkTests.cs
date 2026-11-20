using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LgymApi.Identity.AccountDeletion;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.AccountDeletion;

namespace LgymApi.IntegrationTests;

[TestFixture]
[Category("PostgreSql")]
internal sealed class PostgreSqlAccountDeletionSharedUnitOfWorkTests : PostgreSqlIntegrationTestBase
{
    [Test]
    public async Task ProtectedWrite_AuthorizedBeforeDeletion_RollsBackAfterAccountBecomesPending()
    {
        var user = await SeedUserAsync($"fenced-{Id<User>.New()}", $"fenced-{Id<User>.New()}@example.test");
        await using var staleScope = Factory.Services.CreateAsyncScope();
        var staleDatabase = staleScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await staleDatabase.Users.SingleAsync(candidate => candidate.Id == user.Id);
        await using (var deletingScope = Factory.Services.CreateAsyncScope())
        {
            await deletingScope.ServiceProvider.GetRequiredService<IAccountDeletionService>()
                .RequestAsync(user.Id.Rebind<AccountReference>());
        }
        var measurementId = Id<Measurement>.New();
        staleDatabase.Measurements.Add(new Measurement
        {
            Id = measurementId, UserId = user.Id, Value = 42, Unit = "cm"
        });

        var save = () => staleDatabase.SaveChangesAsync();

        await Assert.ThatAsync(async () => { await save(); }, Throws.InstanceOf<DbUpdateConcurrencyException>());
        await using var verifyScope = Factory.Services.CreateAsyncScope();
        var database = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That((await database.Measurements.AnyAsync(measurement => measurement.Id == measurementId)), Is.False);
        Assert.That((await database.Users.SingleAsync(candidate => candidate.Id == user.Id)).DeletionState, Is.EqualTo(AccountDeletionState.PendingDeletion));
    }

    [Test]
    public async Task DeleteAccountAsync_CommitsPendingStateAndRetainsRecoveryDataAtomically()
    {
        var user = await SeedUserAsync(
            $"postgres-shared-delete-{Id<PostgreSqlAccountDeletionSharedUnitOfWorkTests>.New():N}",
            $"postgres-shared-delete-{Id<PostgreSqlAccountDeletionSharedUnitOfWorkTests>.New():N}@example.test");
        var installationId = Id<PushInstallation>.New();
        var messageId = Id<PushNotificationMessage>.New();
        await SeedPushDataAsync(user.Id, installationId, messageId);
        var writesWereInvisibleBeforeCommit = false;

        await using (var serviceScope = Factory.Services.CreateAsyncScope())
        {
            var database = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var scopedUser = await database.Users.SingleAsync(candidate => candidate.Id == user.Id);
            var unitOfWork = new ObservedUnitOfWork(new EfUnitOfWork(database), async () =>
            {
                var beforeCommit = await ReadStateAsync(user.Id, installationId, messageId);
                writesWereInvisibleBeforeCommit = beforeCommit.DeletionState == AccountDeletionState.Active
                    && beforeCommit.InstallationExists
                    && beforeCommit.MessageExists;
            });
            var deletionService = new AccountDeletionService(
                database,
                unitOfWork,
                serviceScope.ServiceProvider.GetRequiredService<ILegacyPasswordService>(),
                TimeProvider.System,
                serviceScope.ServiceProvider.GetRequiredService<IAccountDeletionSynchronization>(),
                serviceScope.ServiceProvider.GetRequiredService<IAccountRecoveryEmailOutbox>(),
                serviceScope.ServiceProvider.GetRequiredService<LgymApi.Application.Platform.Contracts.BackgroundCommands.ICommandOutboxWriter>());

            await deletionService.RequestAsync(scopedUser.Id.Rebind<AccountReference>());

            Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(1));
            Assert.That(unitOfWork.BeginTransactionCalls, Is.EqualTo(1));
        }

        Assert.That(writesWereInvisibleBeforeCommit, Is.True);
        var persisted = await ReadStateAsync(user.Id, installationId, messageId);
        Assert.That(persisted.DeletionState, Is.EqualTo(AccountDeletionState.PendingDeletion));
        Assert.That(persisted.InstallationExists, Is.True);
        Assert.That(persisted.MessageExists, Is.True);
    }

    private async Task SeedPushDataAsync(
        Id<User> userId,
        Id<PushInstallation> installationId,
        Id<PushNotificationMessage> messageId)
    {
        await using var setupScope = Factory.Services.CreateAsyncScope();
        var database = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
        database.PushInstallations.Add(new PushInstallation
        {
            Id = installationId,
            UserId = userId,
            InstallationId = $"postgres-shared-delete-{installationId}",
            Platform = "android",
            FcmToken = "postgres-shared-delete-token",
            Environment = "test",
            LastSeenAt = DateTimeOffset.UtcNow
        });
        database.PushNotificationMessages.Add(new PushNotificationMessage
        {
            Id = messageId,
            UserId = userId,
            PushInstallationId = installationId,
            SchemaVersion = 1,
            Type = "account.delete",
            EventId = "postgres-shared-delete-event",
            PayloadJson = "{}",
            Status = PushNotificationStatus.Sent
        });
        await database.SaveChangesAsync();
    }

    private async Task<PersistedDeletionState> ReadStateAsync(
        Id<User> userId,
        Id<PushInstallation> installationId,
        Id<PushNotificationMessage> messageId)
    {
        await using var verificationScope = Factory.Services.CreateAsyncScope();
        var database = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deletionState = await database.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.DeletionState)
            .SingleAsync();
        var installationExists = await database.PushInstallations
            .AsNoTracking()
            .AnyAsync(installation => installation.Id == installationId);
        var messageExists = await database.PushNotificationMessages
            .AsNoTracking()
            .AnyAsync(message => message.Id == messageId);
        return new PersistedDeletionState(deletionState, installationExists, messageExists);
    }

    private sealed record PersistedDeletionState(AccountDeletionState DeletionState, bool InstallationExists, bool MessageExists);
}
