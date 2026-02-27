namespace LgymApi.BackgroundWorker.Common.Jobs;

public interface IWelcomeEmailJob
{
    Task ExecuteAsync(Guid notificationId);
}
