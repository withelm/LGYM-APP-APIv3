using LgymApi.Application.Features.Reporting;
using LgymApi.BackgroundWorker.Common.Jobs;

namespace LgymApi.BackgroundWorker.Jobs;

public sealed class ExpiredPhotoUploadCleanupJob : IExpiredPhotoUploadCleanupJob
{
    private readonly IExpiredPhotoUploadCleanupService _cleanupService;

    public ExpiredPhotoUploadCleanupJob(IExpiredPhotoUploadCleanupService cleanupService)
    {
        _cleanupService = cleanupService;
    }

    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return _cleanupService.CleanupExpiredUploadsAsync(cancellationToken);
    }
}
