using LgymApi.Application.Notifications.Contracts.Push;
using LgymApi.Infrastructure.Options;

namespace LgymApi.Infrastructure.Notifications.Push;

public sealed class PushNotificationDeliveryRetrySettings : IPushNotificationDeliveryRetrySettings
{
    private readonly PushNotificationOptions _options;

    public PushNotificationDeliveryRetrySettings(PushNotificationOptions options)
    {
        _options = options;
    }

    public IReadOnlyList<int> RetryDelaysSeconds => _options.RetryDelaysSeconds;
}
