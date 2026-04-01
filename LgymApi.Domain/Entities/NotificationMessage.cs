using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public abstract class NotificationMessageBase<TType, TDerived> : EntityBase<TDerived>
    where TType : notnull
    where TDerived : NotificationMessageBase<TType, TDerived>
{
    public NotificationChannel Channel { get; set; } = NotificationChannel.Email;
    public TType Type { get; set; } = default!;
    public Id<CorrelationScope> CorrelationId { get; set; }
    public Email Recipient { get; set; } = new("placeholder@example.com");
    public string PayloadJson { get; set; } = string.Empty;
    public EmailNotificationStatus Status { get; set; } = EmailNotificationStatus.Pending;
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>
    /// Timestamp when the notification was dispatched to the background scheduler.
    /// Marks transition from Pending to Dispatched state in the durable-intent lifecycle.
    /// </summary>
    public DateTimeOffset? DispatchedAt { get; set; }

    /// <summary>
    /// Background scheduler job ID or queue reference assigned when dispatched.
    /// Enables correlation with external scheduler state (e.g., Hangfire job ID).
    /// </summary>
    public string? SchedulerJobId { get; set; }

    /// <summary>
    /// Indicates whether this notification was dead-lettered after retry budget exhaustion.
    /// Terminal poison state marker.
    /// </summary>
    public bool IsDeadLettered { get; set; }

    /// <summary>
    /// Reason or error message explaining why this notification was dead-lettered.
    /// </summary>
    public string? DeadLetterReason { get; set; }
}

public sealed class NotificationMessage : NotificationMessageBase<EmailNotificationType, NotificationMessage>
{
}
