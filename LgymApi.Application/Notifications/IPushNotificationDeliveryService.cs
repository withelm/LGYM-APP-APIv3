using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Notifications;

public interface IPushNotificationDeliveryService
{
    Task ProcessAsync(
        Id<PushNotificationMessage> notificationId,
        CancellationToken cancellationToken = default);
}
