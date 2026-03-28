using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.BackgroundWorker.Common;

public interface IEmailBackgroundScheduler
{
    void Enqueue(Id<NotificationMessage> notificationId);
}
