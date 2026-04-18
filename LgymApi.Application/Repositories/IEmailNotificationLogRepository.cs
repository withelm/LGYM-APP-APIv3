using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Repositories;

public interface IEmailNotificationLogRepository
{
    Task AddAsync(NotificationMessage message, CancellationToken cancellationToken = default);
    Task<NotificationMessage?> FindByIdAsync(Id<NotificationMessage> id, CancellationToken cancellationToken = default);
    Task<NotificationMessage?> FindByCorrelationAsync(EmailNotificationType type, Id<CorrelationScope> correlationId, string recipient, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all notification messages with Pending status that have not yet been dispatched.
    /// Used for operational observability of queued-but-not-yet-scheduled notifications.
    /// </summary>
    Task<List<NotificationMessage>> GetPendingUndispatchedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all notification messages with Failed status for operational visibility.
    /// Includes both retry-eligible failures and those awaiting retry scheduling.
    /// </summary>
    Task<List<NotificationMessage>> GetFailedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves dispatched notification messages that still retain a scheduler job identifier.
    /// Used by recoverability inspection to correlate durable notification state with scheduler state.
    /// </summary>
    Task<List<NotificationMessage>> GetDispatchedWithSchedulerJobAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all dead-lettered notification messages (terminal poison state).
    /// Used for operational alerts and troubleshooting stranded notifications.
    /// </summary>
    Task<List<NotificationMessage>> GetDeadLetteredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts notification messages by status for operational metrics and reporting.
    /// </summary>
    Task<int> CountByStatusAsync(EmailNotificationStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes sent notification messages older than the specified cutoff date.
    /// Bounded cleanup prevents unbounded table growth while preserving recent audit trail.
    /// Returns the count of deleted records.
    /// </summary>
    Task<int> DeleteSentOlderThanAsync(DateTimeOffset cutoffDate, CancellationToken cancellationToken = default);
}
