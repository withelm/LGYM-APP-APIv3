namespace LgymApi.BackgroundWorker.Common.Notifications;

public interface IEmailJobHandler
{
    Task ProcessAsync(Guid notificationId, CancellationToken cancellationToken = default);
}
