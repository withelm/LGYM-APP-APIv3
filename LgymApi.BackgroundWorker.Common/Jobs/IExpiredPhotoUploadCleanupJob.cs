namespace LgymApi.BackgroundWorker.Common.Jobs;

public interface IExpiredPhotoUploadCleanupJob
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
