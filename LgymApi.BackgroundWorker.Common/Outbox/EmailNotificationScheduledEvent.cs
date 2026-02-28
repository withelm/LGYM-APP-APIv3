namespace LgymApi.BackgroundWorker.Common.Outbox;

public sealed record EmailNotificationScheduledEvent(
    Guid NotificationId,
    Guid CorrelationId,
    string Recipient,
    string NotificationType);
