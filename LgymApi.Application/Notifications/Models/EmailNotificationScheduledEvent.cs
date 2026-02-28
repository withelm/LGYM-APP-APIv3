namespace LgymApi.Application.Notifications.Models;

public sealed record EmailNotificationScheduledEvent(
    Guid NotificationId,
    Guid CorrelationId,
    string Recipient,
    string NotificationType);
