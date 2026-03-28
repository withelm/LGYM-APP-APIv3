using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.BackgroundWorker.Common.Notifications;

public interface IEmailJobHandler
{
    Task ProcessAsync(Id<NotificationMessage> notificationId, CancellationToken cancellationToken = default);
}
