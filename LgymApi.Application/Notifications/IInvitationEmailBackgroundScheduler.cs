namespace LgymApi.Application.Notifications;

public interface IInvitationEmailBackgroundScheduler
{
    void Enqueue(Guid notificationId);
}
