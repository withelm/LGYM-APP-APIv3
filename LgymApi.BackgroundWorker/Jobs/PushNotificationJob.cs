using Hangfire;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.BackgroundWorker.Push;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.BackgroundWorker.Jobs;

[AutomaticRetry(Attempts = 0)]
public sealed class PushNotificationJob : IPushNotificationJob
{
    private readonly PushNotificationJobHandlerService _handler;

    public PushNotificationJob(PushNotificationJobHandlerService handler)
    {
        _handler = handler;
    }

    public Task ExecuteAsync(Id<PushNotificationMessage> notificationId, CancellationToken cancellationToken = default)
    {
        return _handler.ProcessAsync(notificationId, cancellationToken);
    }
}
