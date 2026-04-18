using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Notifications;

public interface IEmailNotificationRecoverabilityRemediationService
{
    Task<IReadOnlyList<Id<NotificationMessage>>> ResetRecoverableNotificationsAsync(
        IReadOnlyList<Id<NotificationMessage>> notificationIds,
        CancellationToken cancellationToken = default);
}
