using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Notifications;

internal sealed class EmailNotificationRecoverabilityRemediationService : IEmailNotificationRecoverabilityRemediationService
{
    private readonly IEmailNotificationLogRepository _notificationLogRepository;
    private readonly IUnitOfWork _unitOfWork;

    public EmailNotificationRecoverabilityRemediationService(
        IEmailNotificationLogRepository notificationLogRepository,
        IUnitOfWork unitOfWork)
    {
        _notificationLogRepository = notificationLogRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<Id<NotificationMessage>>> ResetRecoverableNotificationsAsync(
        IReadOnlyList<Id<NotificationMessage>> notificationIds,
        CancellationToken cancellationToken = default)
    {
        var requeued = new List<Id<NotificationMessage>>();

        foreach (var notificationId in notificationIds)
        {
            var notification = await _notificationLogRepository.FindByIdAsync(notificationId, cancellationToken);
            if (notification == null)
            {
                continue;
            }

            if (notification.Status != EmailNotificationStatus.Pending || notification.IsDeadLettered)
            {
                continue;
            }

            notification.DispatchedAt = null;
            notification.SchedulerJobId = null;
            notification.Status = EmailNotificationStatus.Pending;
            requeued.Add(notificationId);
        }

        if (requeued.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return requeued;
    }
}
