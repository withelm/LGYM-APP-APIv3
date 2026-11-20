using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.BackgroundWorker.Push;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class PushNotificationInstallationIsolationIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task QueuedNotification_WhenInstallationSessionIsRevoked_DoesNotReachProvider()
    {
        var user = await SeedUserAsync("push-revoked-session", "push-revoked-session@example.com", "pass123");
        await LoginAndAuthorizeAsync("push-revoked-session", "pass123");
        var register = await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = "revoked-session-installation",
            platform = "android",
            fcmToken = "revoked-session-token",
            environment = "testing",
            permissionStatus = "authorized"
        });
        Assert.That(register.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        Id<PushNotificationMessage> messageId;
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var installation = await database.PushInstallations.SingleAsync(item => item.InstallationId == "revoked-session-installation");
            var pushNotifications = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
            await pushNotifications.EnqueueAsync(new EnqueuePushNotificationInput(
                user.Id,
                1,
                "push.revoked.session",
                "event-revoked-session",
                null,
                null,
                null));
            messageId = await database.PushNotificationMessages
                .Where(message => message.EventId == "event-revoked-session")
                .Select(message => message.Id)
                .SingleAsync();
            var session = await database.UserSessions.SingleAsync(item => item.Id == installation.SessionId);
            session.RevokedAtUtc = DateTimeOffset.UtcNow;
            await database.SaveChangesAsync();
        }

        using (var scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<PushNotificationJobHandlerService>()
                .ProcessAsync(messageId.ToString());
        }

        Assert.That(Factory.PushSender.Attempts, Is.Empty);
        using var assertionScope = Factory.Services.CreateScope();
        var assertionDatabase = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var message = await assertionDatabase.PushNotificationMessages.SingleAsync(item => item.Id == messageId);
        Assert.That(message.Status, Is.EqualTo(PushNotificationStatus.Failed));
        Assert.That(message.FailureKind, Is.EqualTo(PushNotificationFailureKind.Permanent));
        Assert.That(message.ProviderStatus, Is.EqualTo("InstallationSessionInvalid"));
    }

    [Test]
    public async Task QueuedNotification_WhenInstallationIsReboundAfterLogout_DeliversOnlyTheNewUsersEvent()
    {
        const string installationKey = "shared-installation";
        const string aEventId = "event-owned-by-a";
        const string bEventId = "event-owned-by-b";
        var userA = await SeedUserAsync("push-isolation-a", "push-isolation-a@example.com", "pass123");
        var userB = await SeedUserAsync("push-isolation-b", "push-isolation-b@example.com", "pass123");

        await LoginAndAuthorizeAsync("push-isolation-a", "pass123");
        var registerAResponse = await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = installationKey,
            platform = "android",
            fcmToken = "token-owned-by-a",
            environment = "testing",
            permissionStatus = "authorized"
        });
        Assert.That(registerAResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        Id<PushInstallation> installationId;
        Id<PushNotificationMessage> aMessageId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var installation = await db.PushInstallations.SingleAsync(item => item.InstallationId == installationKey);
            installationId = installation.Id;
            Assert.That(installation.UserId, Is.EqualTo(userA.Id));

            var pushNotifications = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
            await pushNotifications.EnqueueAsync(new EnqueuePushNotificationInput(
                userA.Id,
                1,
                "push.installation.isolation",
                aEventId,
                null,
                null,
                null));
            aMessageId = await db.PushNotificationMessages
                .Where(message => message.EventId == aEventId)
                .Select(message => message.Id)
                .SingleAsync();
        }

        var logoutResponse = await Client.PostAsync("/api/logout", null);
        Assert.That(logoutResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var loggedOutInstallation = await db.PushInstallations.SingleAsync(item => item.InstallationId == installationKey);
            Assert.That(loggedOutInstallation.Id, Is.EqualTo(installationId));
            Assert.That(loggedOutInstallation.UserId, Is.Null);
            Assert.That(loggedOutInstallation.SessionId, Is.Null);
        }

        await LoginAndAuthorizeAsync("push-isolation-b", "pass123");
        var registerBResponse = await Client.PostAsJsonAsync("/api/push/installations/register", new
        {
            installationId = installationKey,
            platform = "android",
            fcmToken = "token-owned-by-b",
            environment = "testing",
            permissionStatus = "authorized"
        });
        Assert.That(registerBResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        Id<PushNotificationMessage> bMessageId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var reboundInstallation = await db.PushInstallations.SingleAsync(item => item.InstallationId == installationKey);
            Assert.That(reboundInstallation.Id, Is.EqualTo(installationId));
            Assert.That(reboundInstallation.UserId, Is.EqualTo(userB.Id));
            Assert.That(reboundInstallation.SessionId, Is.Not.Null);

            var pushNotifications = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
            await pushNotifications.EnqueueAsync(new EnqueuePushNotificationInput(
                userB.Id,
                1,
                "push.installation.isolation",
                bEventId,
                null,
                null,
                null));
            bMessageId = await db.PushNotificationMessages
                .Where(message => message.EventId == bEventId)
                .Select(message => message.Id)
                .SingleAsync();
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<PushNotificationJobHandlerService>();
            await handler.ProcessAsync(aMessageId.ToString());
            await handler.ProcessAsync(bMessageId.ToString());
        }

        Assert.That(Factory.PushSender.Attempts, Has.Exactly(1).Items);
        Assert.That(Factory.PushSender.Attempts.Single().Target.InstallationId, Is.EqualTo(installationId));
        Assert.That(Factory.PushSender.Attempts.Single().Payload.EventId, Is.EqualTo(bEventId));

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var aMessage = await db.PushNotificationMessages.SingleAsync(message => message.Id == aMessageId);
            var bMessage = await db.PushNotificationMessages.SingleAsync(message => message.Id == bMessageId);
            Assert.That(aMessage.Status, Is.EqualTo(PushNotificationStatus.Failed));
            Assert.That(aMessage.FailureKind, Is.EqualTo(PushNotificationFailureKind.Permanent));
            Assert.That(aMessage.ProviderStatus, Is.EqualTo("InstallationUserMismatch"));
            Assert.That(aMessage.NextAttemptAt, Is.Null);
            Assert.That(aMessage.SchedulerJobId, Is.Null);
            Assert.That(bMessage.Status, Is.EqualTo(PushNotificationStatus.Skipped));
        }
    }

    private async Task LoginAndAuthorizeAsync(string name, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/login", new { name, password });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(login, Is.Not.Null);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
    }

    private sealed class LoginResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }
}
