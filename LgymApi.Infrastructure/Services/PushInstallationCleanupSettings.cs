using LgymApi.Application.Notifications;
using LgymApi.Infrastructure.Options;

namespace LgymApi.Infrastructure.Services;

public sealed class PushInstallationCleanupSettings : IStalePushInstallationCleanupSettings
{
    private readonly PushNotificationOptions _options;

    public PushInstallationCleanupSettings(PushNotificationOptions options)
    {
        _options = options;
    }

    public bool Enabled => _options.StaleTokenCleanupEnabled;
    public int InactivityDays => _options.StaleTokenInactivityDays;
    public int BatchSize => _options.StaleTokenCleanupBatchSize;
}
