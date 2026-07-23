using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Notifications.Contracts.Push;

public interface IPushBackgroundScheduler
{
    string? Enqueue(Id<PushNotificationMessage> notificationId);
    string? ScheduleRetry(Id<PushNotificationMessage> notificationId, TimeSpan delay);
}
