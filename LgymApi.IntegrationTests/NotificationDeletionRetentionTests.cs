using LgymApi.Application.Notifications;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class NotificationDeletionRetentionTests
{
    [Test]
    public async Task Cleanup_PreservesProtectedParticipantsWithoutStarvingActiveCandidates()
    {
        using var baseFactory = new CustomWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("PushNotifications:RetentionPurgeBatchSize", "1");
            builder.UseSetting("PushNotifications:StaleTokenCleanupBatchSize", "1");
        });
        var pending = Id<User>.New();
        var finalizing = Id<User>.New();
        var active = Id<User>.New();
        var old = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var protectedMessage = Id<PushNotificationMessage>.New();
        var activeMessage = Id<PushNotificationMessage>.New();
        var protectedInstallation = Id<PushInstallation>.New();
        var activeInstallation = Id<PushInstallation>.New();
        var protectedStale = Id<PushInstallation>.New();
        var activeStale = Id<PushInstallation>.New();
        var protectedNotification = Id<InAppNotification>.New();
        var activeNotification = Id<InAppNotification>.New();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.AddRange(
                new User { Id = pending, Name = "pending", Email = "pending-retention@example.test", DeletionState = AccountDeletionState.PendingDeletion },
                new User { Id = finalizing, Name = "finalizing", Email = "finalizing-retention@example.test", DeletionState = AccountDeletionState.Finalizing },
                new User { Id = active, Name = "active", Email = "active-retention@example.test" });
            db.PushInstallations.AddRange(
                new PushInstallation { Id = protectedInstallation, UserId = pending, InstallationId = "protected-disabled", FcmToken = "protected-disabled", DisabledAt = old, LastSeenAt = old },
                new PushInstallation { Id = activeInstallation, UserId = active, InstallationId = "active-disabled", FcmToken = "active-disabled", DisabledAt = old.AddDays(1), LastSeenAt = old },
                new PushInstallation { Id = protectedStale, UserId = finalizing, InstallationId = "protected-stale", FcmToken = "protected-stale", LastSeenAt = old },
                new PushInstallation { Id = activeStale, UserId = active, InstallationId = "active-stale", FcmToken = "active-stale", LastSeenAt = old.AddDays(1) });
            db.PushNotificationMessages.AddRange(
                new PushNotificationMessage { Id = protectedMessage, UserId = pending, PushInstallationId = protectedInstallation, CreatedAt = old },
                new PushNotificationMessage { Id = activeMessage, UserId = active, PushInstallationId = activeStale, CreatedAt = old.AddDays(1) });
            db.InAppNotifications.AddRange(
                new InAppNotification { Id = protectedNotification, RecipientId = active, SenderUserId = finalizing, CreatedAt = old },
                new InAppNotification { Id = activeNotification, RecipientId = active, CreatedAt = old.AddDays(1) });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            Assert.That((await scope.ServiceProvider.GetRequiredService<IPushNotificationMessageRetentionCleanupService>().CleanupAsync()), Is.EqualTo(1));
            Assert.That((await scope.ServiceProvider.GetRequiredService<IDisabledPushInstallationRetentionCleanupService>().CleanupAsync()), Is.EqualTo(1));
            Assert.That((await scope.ServiceProvider.GetRequiredService<IInAppNotificationRetentionCleanupService>().CleanupAsync()), Is.EqualTo(1));
            Assert.That((await scope.ServiceProvider.GetRequiredService<IStalePushInstallationCleanupService>().CleanupAsync()), Is.EqualTo(1));
        }

        using var verification = factory.Services.CreateScope();
        var result = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var assertedChain9 = (await result.PushNotificationMessages.Select(x => x.Id).ToListAsync());
        Assert.That(assertedChain9, Does.Contain(protectedMessage));
        Assert.That(assertedChain9, Does.Not.Contain(activeMessage));
        var assertedChain10 = (await result.InAppNotifications.Select(x => x.Id).ToListAsync());
        Assert.That(assertedChain10, Does.Contain(protectedNotification));
        Assert.That(assertedChain10, Does.Not.Contain(activeNotification));
        var installations = await result.PushInstallations.ToListAsync();
        var assertedChain11 = installations.Select(x => x.Id);
        Assert.That(assertedChain11, Does.Contain(protectedInstallation));
        Assert.That(assertedChain11, Does.Not.Contain(activeInstallation));
        Assert.That(installations.Single(x => x.Id == protectedStale).DisabledAt, Is.Null);
        Assert.That(installations.Single(x => x.Id == activeStale).DisabledAt, Is.Not.Null);
    }
}
