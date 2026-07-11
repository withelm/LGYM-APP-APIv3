using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class PushNotificationMessage : EntityBase<PushNotificationMessage>
{
    public Id<User> UserId { get; set; }
    public Id<PushInstallation> PushInstallationId { get; set; }
    public int SchemaVersion { get; set; }
    public string Type { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public Id<InAppNotification>? InAppNotificationId { get; set; }
    public string? Deeplink { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public PushNotificationStatus Status { get; set; } = PushNotificationStatus.Pending;
    public PushNotificationFailureKind FailureKind { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? LastError { get; set; }
    public string? ProviderStatus { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? ProviderErrorCode { get; set; }
    public string? ProviderResponseSummary { get; set; }
    public string? SchedulerJobId { get; set; }
}
