using LgymApi.Domain.Enums;

namespace LgymApi.Domain.Entities;

public sealed class NotificationMessage : EntityBase
{
    public NotificationChannel Channel { get; set; } = NotificationChannel.Email;
    public string Type { get; set; } = string.Empty;
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
