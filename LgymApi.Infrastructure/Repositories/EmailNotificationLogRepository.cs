using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class EmailNotificationLogRepository : IEmailNotificationLogRepository
{
    private readonly AppDbContext _dbContext;

    public EmailNotificationLogRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
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
}
