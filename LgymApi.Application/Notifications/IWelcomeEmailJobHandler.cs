namespace LgymApi.Application.Notifications;

public interface IWelcomeEmailJobHandler
{
    Task ProcessAsync(Guid notificationId, CancellationToken cancellationToken = default);
}
