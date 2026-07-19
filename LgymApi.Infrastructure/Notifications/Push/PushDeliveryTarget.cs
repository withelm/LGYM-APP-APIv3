namespace LgymApi.Infrastructure.Notifications.Push;

public sealed record PushDeliveryTarget(
    string InstallationId,
    string DeviceToken);
