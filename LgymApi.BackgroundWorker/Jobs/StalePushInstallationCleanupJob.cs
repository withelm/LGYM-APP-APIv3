using LgymApi.Application.Notifications;
using LgymApi.BackgroundWorker.Common.Jobs;

namespace LgymApi.BackgroundWorker.Jobs;

public sealed class StalePushInstallationCleanupJob : IStalePushInstallationCleanupJob
{
    private readonly IStalePushInstallationCleanupService _cleanupService;

    public StalePushInstallationCleanupJob(IStalePushInstallationCleanupService cleanupService)
    {
        _cleanupService = cleanupService;
    }

    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return _cleanupService.CleanupAsync(cancellationToken);
    }
}
