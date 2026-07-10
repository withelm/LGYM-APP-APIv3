using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class EmailNotificationLogRepository : IEmailNotificationLogRepository
{
    private readonly AppDbContext _dbContext;
    private readonly int _emailSendLeaseSeconds;

    public EmailNotificationLogRepository(AppDbContext dbContext, BackgroundCommandOptions? options = null)
    {
        _dbContext = dbContext;
        _emailSendLeaseSeconds = options?.EmailSendLeaseSeconds ?? 30;
    }

    public async Task AddAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        await _dbContext.NotificationMessages.AddAsync(message, cancellationToken);
    }

    public Task<NotificationMessage?> FindByIdAsync(Id<NotificationMessage> id, CancellationToken cancellationToken = default)
    {
        return _dbContext.NotificationMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<NotificationMessage?> FindByCorrelationAsync(EmailNotificationType type, Id<CorrelationScope> correlationId, string recipient, CancellationToken cancellationToken = default)
    {
        return _dbContext.NotificationMessages.FirstOrDefaultAsync(
            x => x.Channel == NotificationChannel.Email && x.Type == type && x.CorrelationId == correlationId && x.Recipient == recipient,
            cancellationToken);
    }

    /// <summary>
    /// Retrieves all notification messages with Pending status that have not yet been dispatched.
    /// Used for operational observability of queued-but-not-yet-scheduled notifications.
    /// </summary>
    public async Task<List<NotificationMessage>> GetPendingUndispatchedAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.NotificationMessages
            .Where(x => x.Status == EmailNotificationStatus.Pending && x.DispatchedAt == null)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves all notification messages with Failed status for operational visibility.
    /// Includes both retry-eligible failures and those awaiting retry scheduling.
    /// </summary>
    public async Task<List<NotificationMessage>> GetFailedAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.NotificationMessages
            .Where(x => x.Status == EmailNotificationStatus.Failed)
            .OrderBy(x => x.LastAttemptAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves all dead-lettered notification messages (terminal poison state).
    /// Used for operational alerts and troubleshooting stranded notifications.
    /// </summary>
    public async Task<List<NotificationMessage>> GetDeadLetteredAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.NotificationMessages
            .Where(x => x.IsDeadLettered)
            .OrderBy(x => x.LastAttemptAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Counts notification messages by status for operational metrics and reporting.
    /// </summary>
    public async Task<int> CountByStatusAsync(EmailNotificationStatus status, CancellationToken cancellationToken = default)
    {
        return await _dbContext.NotificationMessages
            .Where(x => x.Status == status)
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// Deletes sent notification messages older than the specified cutoff date.
    /// Bounded cleanup prevents unbounded table growth while preserving recent audit trail.
    /// Returns the count of deleted records.
    /// </summary>
    public async Task<int> DeleteSentOlderThanAsync(DateTimeOffset cutoffDate, CancellationToken cancellationToken = default)
    {
        var messagesToDelete = await _dbContext.NotificationMessages
            .Where(x => x.Status == EmailNotificationStatus.Sent && x.SentAt != null && x.SentAt < cutoffDate)
            .ToListAsync(cancellationToken);

        foreach (var message in messagesToDelete)
        {
            _dbContext.NotificationMessages.Remove(message);
        }

        return messagesToDelete.Count;
    }

    /// <summary>
    /// Atomically claims a notification for sending by transitioning it from Pending to Sending.
    /// The status change is a set-based atomic update guarded by the current Pending status, so
    /// concurrent dispatchers cannot double-claim the same notification. The send lease starts at
    /// the moment of the claim (LastAttemptAt is stamped) to enable stuck-sending detection.
    /// Returns true when this call won the claim.
    /// </summary>
    /// <remarks>
    /// Crash recovery: a notification stranded in the Sending state whose send lease has expired
    /// (DeliveredAt is still null, meaning the send never completed) is also reclaimed. This lets a
    /// subsequent dispatcher finish a send that crashed between the claim and the SMTP call, while a
    /// still-fresh Sending lease (an in-flight concurrent dispatcher) is left untouched so it is not
    /// double-claimed.
    /// </remarks>
    public async Task<bool> TryTransitionToSendingAsync(Id<NotificationMessage> id, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var leaseCutoff = now.AddSeconds(-_emailSendLeaseSeconds);

        // Pending -> Sending claim (original behavior).
        var claimed = await _dbContext.NotificationMessages
            .Where(x => x.Id == id && x.Status == EmailNotificationStatus.Pending)
            .StageUpdateAsync(
                _dbContext,
                x => x.Status,
                _ => EmailNotificationStatus.Sending,
                cancellationToken);

        if (claimed > 0)
        {
            await StampLeaseAsync(id, now, cancellationToken);
            return true;
        }

        // Reclaim a notification stuck in Sending with an expired lease that was never delivered.
        // This recovers a send that crashed after the claim but before the SMTP call completed.
        var reclaimed = await _dbContext.NotificationMessages
            .Where(x => x.Id == id
                        && x.Status == EmailNotificationStatus.Sending
                        && x.DeliveredAt == null
                        && x.LastAttemptAt != null
                        && x.LastAttemptAt < leaseCutoff)
            .StageUpdateAsync(
                _dbContext,
                x => x.Status,
                _ => EmailNotificationStatus.Sending,
                cancellationToken);

        if (reclaimed > 0)
        {
            await StampLeaseAsync(id, now, cancellationToken);
            return true;
        }

        return false;
    }

    private async Task StampLeaseAsync(Id<NotificationMessage> id, DateTimeOffset leaseStart, CancellationToken cancellationToken)
    {
        await _dbContext.NotificationMessages
            .Where(x => x.Id == id)
            .StageUpdateAsync(
                _dbContext,
                x => x.LastAttemptAt,
                _ => leaseStart,
                cancellationToken);
    }

    /// <summary>
    /// Returns notifications stranded in the Sending state beyond the configured send lease.
    /// A notification is stuck when its status is Sending and its last attempt timestamp
    /// predates (now - emailSendLeaseSeconds), indicating the owning dispatcher failed to
    /// complete or renew the lease.
    /// </summary>
    public async Task<List<NotificationMessage>> GetStuckSendingAsync(int emailSendLeaseSeconds, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-emailSendLeaseSeconds);

        return await _dbContext.NotificationMessages
            .Where(x => x.Status == EmailNotificationStatus.Sending
                        && x.LastAttemptAt != null
                        && x.LastAttemptAt < cutoff)
            .OrderBy(x => x.LastAttemptAt)
            .ToListAsync(cancellationToken);
    }
}
