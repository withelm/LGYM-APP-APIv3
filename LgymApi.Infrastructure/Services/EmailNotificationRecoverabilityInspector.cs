using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Enums;

namespace LgymApi.Infrastructure.Services;

public sealed class EmailNotificationRecoverabilityInspector : IEmailNotificationRecoverabilityInspector
{
    private readonly IEmailNotificationLogRepository _notificationLogRepository;
    private readonly IHangfireJobStateReader _hangfireJobStateReader;

    public EmailNotificationRecoverabilityInspector(
        IEmailNotificationLogRepository notificationLogRepository,
        IHangfireJobStateReader hangfireJobStateReader)
    {
        _notificationLogRepository = notificationLogRepository;
        _hangfireJobStateReader = hangfireJobStateReader;
    }

    public async Task<EmailNotificationRecoverabilityInspectionResult> InspectAsync(CancellationToken cancellationToken = default)
    {
        var notifications = await _notificationLogRepository.GetDispatchedWithSchedulerJobAsync(cancellationToken);
        var items = new List<EmailNotificationRecoverabilityInspectionItem>(notifications.Count);

        var brokenJobsFound = 0;
        var recoverableNotifications = 0;
        var activeJobsSkipped = 0;
        var alreadySentSkipped = 0;
        var deadLetterSkipped = 0;

        foreach (var notification in notifications)
        {
            var schedulerJobId = notification.SchedulerJobId!;

            if (notification.Status == EmailNotificationStatus.Sent)
            {
                alreadySentSkipped++;
                items.Add(CreateItem(notification, schedulerJobId, EmailNotificationRecoverabilityDisposition.AlreadySentSkipped, null, false));
                continue;
            }

            if (notification.IsDeadLettered)
            {
                deadLetterSkipped++;
                items.Add(CreateItem(notification, schedulerJobId, EmailNotificationRecoverabilityDisposition.DeadLetterSkipped, null, false));
                continue;
            }

            if (notification.Status != EmailNotificationStatus.Pending)
            {
                items.Add(CreateItem(notification, schedulerJobId, EmailNotificationRecoverabilityDisposition.DurableStateSkipped, null, false));
                continue;
            }

            var snapshot = await _hangfireJobStateReader.ReadAsync(schedulerJobId, cancellationToken);

            if (snapshot.IsBroken)
            {
                brokenJobsFound++;
                recoverableNotifications++;
                items.Add(CreateItem(notification, schedulerJobId, EmailNotificationRecoverabilityDisposition.Recoverable, snapshot.StateName, true));
                continue;
            }

            activeJobsSkipped++;
            items.Add(CreateItem(notification, schedulerJobId, EmailNotificationRecoverabilityDisposition.ActiveJobSkipped, snapshot.StateName, false));
        }

        return new EmailNotificationRecoverabilityInspectionResult(
            brokenJobsFound,
            recoverableNotifications,
            activeJobsSkipped,
            alreadySentSkipped,
            deadLetterSkipped,
            items);
    }

    private static EmailNotificationRecoverabilityInspectionItem CreateItem(
        Domain.Entities.NotificationMessage notification,
        string schedulerJobId,
        EmailNotificationRecoverabilityDisposition disposition,
        string? hangfireState,
        bool hasBrokenSchedulerState)
    {
        return new EmailNotificationRecoverabilityInspectionItem(
            notification.Id,
            notification.Type,
            schedulerJobId,
            notification.Status,
            notification.IsDeadLettered,
            disposition,
            hangfireState,
            hasBrokenSchedulerState);
    }
}
