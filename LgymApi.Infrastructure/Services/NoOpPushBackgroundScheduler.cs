using LgymApi.BackgroundWorker.Common.Push;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Infrastructure.Services;

public sealed class NoOpPushBackgroundScheduler : IPushBackgroundScheduler
{
    public string? Enqueue(Id<PushNotificationMessage> notificationId)
    {
        return null;
    }

    public string? ScheduleRetry(Id<PushNotificationMessage> notificationId, TimeSpan delay)
    {
        return null;
    }
}
