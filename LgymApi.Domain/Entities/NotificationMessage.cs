using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;

namespace LgymApi.Domain.Entities;

public abstract class NotificationMessage<TType> : EntityBase
    where TType : notnull
{
    public NotificationChannel Channel { get; set; } = NotificationChannel.Email;
    public TType Type { get; set; } = default!;
    public Guid CorrelationId { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public EmailNotificationStatus Status { get; set; } = EmailNotificationStatus.Pending;
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
}

public sealed class NotificationMessage : NotificationMessage<EmailNotificationType>
{
}
