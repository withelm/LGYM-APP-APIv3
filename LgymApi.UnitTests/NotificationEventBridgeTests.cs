using FluentAssertions;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class NotificationEventBridgeTests
{
    [Test]
    public async Task EnqueueAsync_ForwardsOptionalInAppNotificationId()
    {
        var pushNotificationService = Substitute.For<IPushNotificationService>();
        var bridge = new NotificationEventBridge(pushNotificationService);
        var userId = Id<User>.New();
        var inAppNotificationId = Id<InAppNotification>.New();
        var input = new EnqueueNotificationEventInput(
            userId,
            1,
            "test.event",
            "event-123",
            "entity-123",
            inAppNotificationId,
            "/notifications");

        await bridge.EnqueueAsync(input);

        await pushNotificationService.Received(1).EnqueueAsync(
            Arg.Is<EnqueuePushNotificationInput>(queued =>
                queued.UserId == userId
                && queued.SchemaVersion == 1
                && queued.Type == "test.event"
                && queued.EventId == "event-123"
                && queued.EntityId == "entity-123"
                && queued.InAppNotificationId == inAppNotificationId
                && queued.Deeplink == "/notifications"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EnqueueAsync_AllowsMissingInAppNotificationId()
    {
        var pushNotificationService = Substitute.For<IPushNotificationService>();
        var bridge = new NotificationEventBridge(pushNotificationService);
        var input = new EnqueueNotificationEventInput(
            Id<User>.New(),
            1,
            "test.event",
            "event-456",
            null,
            null,
            null);

        await bridge.EnqueueAsync(input);

        await pushNotificationService.Received(1).EnqueueAsync(
            Arg.Is<EnqueuePushNotificationInput>(queued => queued.InAppNotificationId == null),
            Arg.Any<CancellationToken>());
    }
}
