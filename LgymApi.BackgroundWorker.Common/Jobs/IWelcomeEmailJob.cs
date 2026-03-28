using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.BackgroundWorker.Common.Jobs;

public interface IWelcomeEmailJob
{
    Task ExecuteAsync(Id<NotificationMessage> notificationId);
}
