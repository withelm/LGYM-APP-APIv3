using LgymApi.Domain.Entities;

namespace LgymApi.Application.Notifications.Contracts.Push;

public interface IPushProviderSender
{
    Task<PushSendAttemptResult> SendAsync(
        PushInstallation installation,
        PushEventPayload payload,
        CancellationToken cancellationToken = default);
}
