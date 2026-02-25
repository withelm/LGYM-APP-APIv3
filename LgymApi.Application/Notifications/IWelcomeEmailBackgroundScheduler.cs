namespace LgymApi.Application.Notifications;

public interface IWelcomeEmailBackgroundScheduler
{
    void Enqueue(Guid notificationId);
}
