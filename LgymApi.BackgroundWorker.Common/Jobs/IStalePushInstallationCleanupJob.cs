namespace LgymApi.BackgroundWorker.Common.Jobs;

public interface IStalePushInstallationCleanupJob
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
