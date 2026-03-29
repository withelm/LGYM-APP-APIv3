using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.BackgroundWorker.Common;

public interface IEmailBackgroundScheduler
{
    string? Enqueue(Id<NotificationMessage> notificationId);
}
