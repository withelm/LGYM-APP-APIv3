using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LgymApi.Infrastructure.Services;

public sealed class EmailNotificationRecoverabilityRemediator : IEmailNotificationRecoverabilityRemediator
{
    private readonly IEmailNotificationRecoverabilityInspector _inspector;
    private readonly IHangfireJobReconciler _hangfireJobReconciler;
    private readonly IEmailNotificationRecoverabilityRemediationService _remediationService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailNotificationRecoverabilityRemediator> _logger;

    public EmailNotificationRecoverabilityRemediator(
        IEmailNotificationRecoverabilityInspector inspector,
        IHangfireJobReconciler hangfireJobReconciler,
        IEmailNotificationRecoverabilityRemediationService remediationService,
        IServiceScopeFactory scopeFactory,
        ILogger<EmailNotificationRecoverabilityRemediator> logger)
    {
        _inspector = inspector;
        _hangfireJobReconciler = hangfireJobReconciler;
        _remediationService = remediationService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<EmailNotificationRecoverabilityInspectionResult> RemediateAsync(CancellationToken cancellationToken = default)
    {
        var inspection = await _inspector.InspectAsync(cancellationToken);
        var recoverableItems = inspection.Notifications
            .Where(item => item.Disposition == EmailNotificationRecoverabilityDisposition.Recoverable)
            .ToList();

        if (recoverableItems.Count == 0)
        {
            return inspection;
        }

        var replayTargets = new List<(Id<NotificationMessage> NotificationId, string SchedulerJobId)>();

        foreach (var item in recoverableItems)
        {
            replayTargets.Add((item.NotificationId, item.SchedulerJobId));
        }

        if (replayTargets.Count == 0)
        {
            return inspection;
        }

        var resetIds = await _remediationService.ResetRecoverableNotificationsAsync(
            replayTargets.Select(x => x.NotificationId).ToList(),
            cancellationToken);

        using var refreshScope = _scopeFactory.CreateScope();
        var freshDbContext = refreshScope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var replayTarget in replayTargets.Where(x => resetIds.Contains(x.NotificationId)))
        {
            try
            {
                var notification = await freshDbContext.NotificationMessages
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == replayTarget.NotificationId, cancellationToken);
                if (notification == null || notification.Status != EmailNotificationStatus.Pending)
                {
                    continue;
                }

                var newSchedulerJobId = notification.SchedulerJobId;
                if (string.IsNullOrWhiteSpace(newSchedulerJobId) || string.Equals(newSchedulerJobId, replayTarget.SchedulerJobId, StringComparison.Ordinal))
                {
                    continue;
                }

                var reconciled = await _hangfireJobReconciler.ReconcileAsync(replayTarget.SchedulerJobId, cancellationToken);
                if (!reconciled)
                {
                    _logger.LogWarning(
                        "Hangfire job {SchedulerJobId} was not reconciled after notification {NotificationId} replay.",
                        replayTarget.SchedulerJobId,
                        replayTarget.NotificationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to reconcile stale Hangfire job {SchedulerJobId} after notification {NotificationId} replay.",
                    replayTarget.SchedulerJobId,
                    replayTarget.NotificationId);
            }
        }

        return inspection;
    }
}
