using LgymApi.Application.Notifications;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.BackgroundWorker.Push;

public sealed class PushNotificationJobHandlerService
{
    private readonly IPushNotificationDeliveryService _pushNotificationDeliveryService;

    public PushNotificationJobHandlerService(IPushNotificationDeliveryService pushNotificationDeliveryService)
    {
        _pushNotificationDeliveryService = pushNotificationDeliveryService;
    }

    public Task ProcessAsync(Id<PushNotificationMessage> notificationId, CancellationToken cancellationToken = default)
        => _pushNotificationDeliveryService.ProcessAsync(notificationId, cancellationToken);
}
