namespace LgymApi.BackgroundWorker.Common;

public interface IEmailBackgroundScheduler
{
    void Enqueue(Guid notificationId);
}
