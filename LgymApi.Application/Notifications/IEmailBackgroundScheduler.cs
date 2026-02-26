namespace LgymApi.Application.Notifications;

public interface IEmailBackgroundScheduler
{
    void Enqueue(Guid notificationId);
}
