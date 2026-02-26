namespace LgymApi.Application.Notifications;

public interface IEmailJobHandler
{
    Task ProcessAsync(Guid notificationId, CancellationToken cancellationToken = default);
}
