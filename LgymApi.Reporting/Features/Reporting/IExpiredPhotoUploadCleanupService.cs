namespace LgymApi.Application.Features.Reporting;

public interface IExpiredPhotoUploadCleanupService
{
    Task<int> CleanupExpiredUploadsAsync(CancellationToken cancellationToken = default);
}
