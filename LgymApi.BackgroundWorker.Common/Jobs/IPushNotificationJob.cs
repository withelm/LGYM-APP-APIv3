using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.BackgroundWorker.Common.Jobs;

public interface IPushNotificationJob
{
    Task ExecuteAsync(Id<PushNotificationMessage> notificationId, CancellationToken cancellationToken = default);
}
