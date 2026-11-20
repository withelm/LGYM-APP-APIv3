using System.Net;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class AccountDeletionPushCleanupIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task DeleteAccount_PreservesInstallationsAndMessageHistoryDuringRecoveryWindow()
    {
        var user = await SeedUserAsync(
            name: $"delete-push-{Id<AccountDeletionPushCleanupIntegrationTests>.New():N}",
            email: $"delete-push-{Id<AccountDeletionPushCleanupIntegrationTests>.New():N}@example.test");
        var activeInstallationId = Id<PushInstallation>.New();
        var disabledInstallationId = Id<PushInstallation>.New();
        var softDeletedInstallationId = Id<PushInstallation>.New();
        var unrelatedInstallationId = Id<PushInstallation>.New();
        Id<PushNotificationMessage> activeMessageId;
        Id<PushNotificationMessage> disabledMessageId;
        Id<PushNotificationMessage> softDeletedMessageId;
        Id<PushNotificationMessage> unrelatedMessageId;

        using (var setupScope = Factory.Services.CreateScope())
        {
            var database = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var unrelatedUser = new User
            {
                Id = Id<User>.New(),
                Name = $"unrelated-push-{Id<User>.New():N}",
                Email = $"unrelated-push-{Id<User>.New():N}@example.test",
                ProfileRank = "Junior 1"
            };
            var activeMessage = CreateMessage(activeInstallationId, user.Id, "account-delete-active");
            var disabledMessage = CreateMessage(disabledInstallationId, user.Id, "account-delete-disabled");
            var softDeletedMessage = CreateMessage(softDeletedInstallationId, user.Id, "account-delete-soft-deleted");
            var unrelatedMessage = CreateMessage(unrelatedInstallationId, unrelatedUser.Id, "account-delete-unrelated");
            activeMessageId = activeMessage.Id;
            disabledMessageId = disabledMessage.Id;
            softDeletedMessageId = softDeletedMessage.Id;
            unrelatedMessageId = unrelatedMessage.Id;

            database.PushInstallations.AddRange(
                CreateInstallation(activeInstallationId, user.Id, null),
                CreateInstallation(disabledInstallationId, user.Id, DateTimeOffset.UtcNow.AddDays(-1)),
                CreateInstallation(softDeletedInstallationId, user.Id, null, isDeleted: true),
                CreateInstallation(unrelatedInstallationId, unrelatedUser.Id, null));
            database.Users.Add(unrelatedUser);
            database.PushNotificationMessages.AddRange(
                activeMessage,
                disabledMessage,
                softDeletedMessage,
                unrelatedMessage);
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(user.Id);
        var response = await Client.GetAsync("/api/deleteAccount");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var verificationScope = Factory.Services.CreateScope();
        var verificationDatabase = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pendingUser = await verificationDatabase.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == user.Id);
        var remainingInstallationIds = await verificationDatabase.PushInstallations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(installation => installation.Id)
            .ToListAsync();
        var remainingMessageIds = await verificationDatabase.PushNotificationMessages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(message => message.Id)
            .ToListAsync();

        Assert.That(pendingUser.IsDeleted, Is.False);
        Assert.That(pendingUser.Name, Is.EqualTo(user.Name));
        Assert.That(pendingUser.Email, Is.EqualTo(user.Email));
        Assert.That(remainingInstallationIds, Is.EquivalentTo(new[]
        {
            unrelatedInstallationId, activeInstallationId, disabledInstallationId, softDeletedInstallationId
        }));
        Assert.That(remainingMessageIds, Is.EquivalentTo(new[]
        {
            unrelatedMessageId, activeMessageId, disabledMessageId, softDeletedMessageId
        }));
    }

    private static PushInstallation CreateInstallation(
        Id<PushInstallation> id,
        Id<User> userId,
        DateTimeOffset? disabledAt,
        bool isDeleted = false)
        => new()
        {
            Id = id,
            UserId = userId,
            InstallationId = id.ToString(),
            Platform = "android",
            FcmToken = $"test-token-{id}",
            Environment = "test",
            LastSeenAt = disabledAt ?? DateTimeOffset.UtcNow,
            DisabledAt = disabledAt,
            DisabledReason = disabledAt == null ? null : "InactiveStale",
            IsDeleted = isDeleted
        };

    private static PushNotificationMessage CreateMessage(
        Id<PushInstallation> installationId,
        Id<User> userId,
        string eventId)
        => new()
        {
            Id = Id<PushNotificationMessage>.New(),
            UserId = userId,
            PushInstallationId = installationId,
            SchemaVersion = 1,
            Type = "account.delete",
            EventId = eventId,
            PayloadJson = "{}",
            Status = PushNotificationStatus.Sent
        };
}
