namespace LgymApi.Application.Notifications;

public interface IInvitationEmailJobHandler
{
    Task ProcessAsync(Guid notificationId, CancellationToken cancellationToken = default);
}
