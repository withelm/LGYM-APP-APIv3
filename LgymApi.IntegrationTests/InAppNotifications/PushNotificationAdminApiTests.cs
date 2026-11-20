using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests.InAppNotifications;

[TestFixture]
public sealed class PushNotificationAdminApiTests : IntegrationTestBase
{
    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/internal/push/test-event", "admin", "current-permission-allow")]
    public async Task EnqueueTestEvent_WithLinkedInAppNotification_PersistsPrivacySafePayload()
    {
        var admin = await SeedAdminAsync();
        var recipient = await SeedUserAsync(name: "push-admin-target", email: "push-admin-target@example.com");
        var linkedNotification = await SeedNotificationAsync(recipient.Id, "Sensitive test body that must stay out of push payload");
        await SeedPushInstallationAsync(recipient.Id, "device-admin-test", "token-admin-test");

        SetAuthorizationHeader(admin.Id);
        var response = await PostAsJsonWithApiOptionsAsync(
            "/api/internal/push/test-event",
            new EnqueueTestPushEventHttpRequest(
                recipient.Id.ToString(),
                "internal.test.push",
                "event-admin-test-1",
                "entity-admin-test-1",
                linkedNotification.Id.ToString(),
                "/notifications"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("Push test event queued"));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var message = await db.PushNotificationMessages.SingleAsync(x => x.EventId == "event-admin-test-1");

        Assert.That(message.Type, Is.EqualTo("internal.test.push"));
        Assert.That(message.InAppNotificationId, Is.EqualTo(linkedNotification.Id));
        Assert.That(message.PayloadJson, Does.Contain(linkedNotification.Id.ToString()));
        Assert.That(message.PayloadJson, Does.Contain("event-admin-test-1"));
        Assert.That(message.PayloadJson, Does.Contain("entity-admin-test-1"));
        Assert.That(message.PayloadJson, Does.Not.Contain(linkedNotification.Message));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/internal/push/test-event", "admin", "ordinary-user-denial")]
    public async Task EnqueueTestEvent_NonAdminUser_ReturnsForbidden()
    {
        var user = await SeedUserAsync(name: "push-admin-forbidden", email: "push-admin-forbidden@example.com");
        var recipient = await SeedUserAsync(name: "push-admin-forbidden-target", email: "push-admin-forbidden-target@example.com");
        await SeedPushInstallationAsync(recipient.Id, "device-admin-forbidden", "token-admin-forbidden");

        SetAuthorizationHeader(user.Id);
        var response = await PostAsJsonWithApiOptionsAsync(
            "/api/internal/push/test-event",
            new EnqueueTestPushEventHttpRequest(
                recipient.Id.ToString(),
                "internal.test.push",
                "event-admin-test-2",
                null,
                null,
                null));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo(CompatibilityResourceMessage.InCulture("en", () => Messages.Unauthorized)));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.PushNotificationMessages.CountAsync(x => x.EventId == "event-admin-test-2");
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public async Task EnqueueTestEvent_WithoutAuthorization_ReturnsUnauthorized()
    {
        var response = await PostAsJsonWithApiOptionsAsync(
            "/api/internal/push/test-event",
            new EnqueueTestPushEventHttpRequest(
                Id<User>.New().ToString(),
                "internal.test.push",
                "event-admin-test-anonymous",
                null,
                null,
                null));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo(CompatibilityResourceMessage.InCulture("en", () => Messages.InvalidToken)));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That((await db.PushNotificationMessages.CountAsync(x => x.EventId == "event-admin-test-anonymous")), Is.EqualTo(0));
    }

    private async Task<InAppNotification> SeedNotificationAsync(Id<User> recipientId, string message)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var notification = new InAppNotification
        {
            Id = Id<InAppNotification>.New(),
            RecipientId = recipientId,
            IsSystemNotification = true,
            Message = message,
            RedirectUrl = "/app/notifications",
            IsRead = false,
            Type = InAppNotificationTypes.InvitationSent,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.InAppNotifications.Add(notification);
        await db.SaveChangesAsync();
        return notification;
    }

    private async Task SeedPushInstallationAsync(Id<User> userId, string installationId, string token)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.PushInstallations.Add(new PushInstallation
        {
            Id = Id<PushInstallation>.New(),
            UserId = userId,
            InstallationId = installationId,
            Platform = "android",
            FcmToken = token,
            Environment = "development",
            PermissionStatus = "authorized",
            LastSeenAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private sealed record EnqueueTestPushEventHttpRequest(
        [property: JsonPropertyName("recipientUserId")] string RecipientUserId,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("eventId")] string EventId,
        [property: JsonPropertyName("entityId")] string? EntityId,
        [property: JsonPropertyName("inAppNotificationId")] string? InAppNotificationId,
        [property: JsonPropertyName("deeplink")] string? Deeplink);

    private sealed class MessageResponse
    {
        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }
}
