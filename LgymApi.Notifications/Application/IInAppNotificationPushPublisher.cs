using LgymApi.Notifications.Application.Models;

namespace LgymApi.Notifications.Application;

public interface IInAppNotificationPushPublisher
{
    Task PushAsync(InAppNotificationResult notification, CancellationToken ct = default);
}
