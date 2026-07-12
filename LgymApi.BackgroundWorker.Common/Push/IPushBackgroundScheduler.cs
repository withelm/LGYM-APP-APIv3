using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.BackgroundWorker.Common.Push;

public interface IPushBackgroundScheduler
{
    string? Enqueue(Id<PushNotificationMessage> notificationId);
    string? ScheduleRetry(Id<PushNotificationMessage> notificationId, TimeSpan delay);
}
