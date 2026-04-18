using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LgymApi.Infrastructure.Services;

public sealed class CommittedIntentDispatcher : ICommittedIntentDispatcher
{
    private const int BatchSize = 100;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CommittedIntentDispatcher> _logger;

    public CommittedIntentDispatcher(
        IServiceScopeFactory scopeFactory,
        ILogger<CommittedIntentDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task DispatchCommittedIntentsAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actionScheduler = scope.ServiceProvider.GetRequiredService<IActionMessageScheduler>();
        var emailScheduler = scope.ServiceProvider.GetRequiredService<IEmailBackgroundScheduler>();

        await DispatchCommandEnvelopesAsync(dbContext, actionScheduler, cancellationToken);
        await DispatchNotificationMessagesAsync(dbContext, emailScheduler, cancellationToken);
    }

    private async Task DispatchCommandEnvelopesAsync(
        AppDbContext dbContext,
        IActionMessageScheduler scheduler,
        CancellationToken cancellationToken)
    {
        var pendingEnvelopes = await dbContext.CommandEnvelopes
            .Where(x => x.Status == ActionExecutionStatus.Pending && x.DispatchedAt == null)
            .OrderBy(x => x.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var envelope in pendingEnvelopes)
        {
            try
            {
                var schedulerJobId = scheduler.Enqueue(envelope.Id);
                envelope.DispatchedAt = DateTimeOffset.UtcNow;
                envelope.SchedulerJobId = schedulerJobId;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to dispatch committed command envelope {EnvelopeId}. It remains recoverable as undispatched.",
                    envelope.Id);
            }
        }
    }

    private async Task DispatchNotificationMessagesAsync(
        AppDbContext dbContext,
        IEmailBackgroundScheduler scheduler,
        CancellationToken cancellationToken)
    {
        var pendingNotifications = await dbContext.NotificationMessages
            .Where(x => x.Status == EmailNotificationStatus.Pending && x.DispatchedAt == null && !x.IsDeadLettered)
            .OrderBy(x => x.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var notification in pendingNotifications)
        {
            try
            {
                var schedulerJobId = scheduler.Enqueue(notification.Id);
                notification.DispatchedAt = DateTimeOffset.UtcNow;
                notification.SchedulerJobId = schedulerJobId;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to dispatch committed notification message {NotificationId}. It remains recoverable as undispatched.",
                    notification.Id);
            }
        }
    }

    private async Task<string?> DispatchNotificationMessageAsync(
        AppDbContext dbContext,
        IEmailBackgroundScheduler scheduler,
        Id<NotificationMessage> notificationId,
        CancellationToken cancellationToken)
    {
        var notification = await dbContext.NotificationMessages
            .Where(x => x.Id == notificationId && x.Status == EmailNotificationStatus.Pending && x.DispatchedAt == null && !x.IsDeadLettered)
            .SingleOrDefaultAsync(cancellationToken);

        if (notification == null)
        {
            return null;
        }

        try
        {
            var schedulerJobId = scheduler.Enqueue(notification.Id);
            notification.DispatchedAt = DateTimeOffset.UtcNow;
            notification.SchedulerJobId = schedulerJobId;
            await dbContext.SaveChangesAsync(cancellationToken);
            return schedulerJobId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to dispatch committed notification message {NotificationId}. It remains recoverable as undispatched.",
                notification.Id);
            return null;
        }
    }
}
