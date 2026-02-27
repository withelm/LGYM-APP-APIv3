namespace LgymApi.BackgroundWorker.Common.Jobs;

public interface IEmailJob
{
    Task ExecuteAsync(Guid notificationId);
}
