using LgymApi.Application.Notifications.Contracts.Push;

namespace LgymApi.Infrastructure.Notifications.Push;

public interface IPushProviderSender
{
    Task<PushSendAttemptResult> SendAsync(
        PushDeliveryTarget target,
        PushEventPayload payload,
        CancellationToken cancellationToken = default);
}
