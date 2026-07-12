using LgymApi.BackgroundWorker.Common.Push.Models;
using LgymApi.Domain.Entities;

namespace LgymApi.BackgroundWorker.Common.Push;

public interface IPushProviderSender
{
    Task<PushSendAttemptResult> SendAsync(
        PushInstallation installation,
        PushEventPayload payload,
        CancellationToken cancellationToken = default);
}
