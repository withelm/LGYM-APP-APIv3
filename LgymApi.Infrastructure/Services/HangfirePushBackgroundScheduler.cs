using Hangfire;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.BackgroundWorker.Common.Push;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Infrastructure.Services;

public sealed class HangfirePushBackgroundScheduler : IPushBackgroundScheduler
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public HangfirePushBackgroundScheduler(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public string? Enqueue(Id<PushNotificationMessage> notificationId)
    {
        return _backgroundJobClient.Enqueue<IPushNotificationJob>(job => job.ExecuteAsync(notificationId, CancellationToken.None));
    }

    public string? ScheduleRetry(Id<PushNotificationMessage> notificationId, TimeSpan delay)
    {
        return _backgroundJobClient.Schedule<IPushNotificationJob>(job => job.ExecuteAsync(notificationId, CancellationToken.None), delay);
    }
}
