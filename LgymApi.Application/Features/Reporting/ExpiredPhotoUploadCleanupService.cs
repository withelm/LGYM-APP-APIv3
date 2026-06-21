using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LgymApi.Application.Features.Reporting;

public sealed class ExpiredPhotoUploadCleanupService : IExpiredPhotoUploadCleanupService
{
    private readonly IPhotoUploadInitTracker _photoUploadInitTracker;
    private readonly IPhotoStorageProvider _photoStorageProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ExpiredPhotoUploadCleanupService> _logger;

    public ExpiredPhotoUploadCleanupService(
        IPhotoUploadInitTracker photoUploadInitTracker,
        IPhotoStorageProvider photoStorageProvider,
        IUnitOfWork unitOfWork,
        ILogger<ExpiredPhotoUploadCleanupService> logger)
    {
        _photoUploadInitTracker = photoUploadInitTracker;
        _photoStorageProvider = photoStorageProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<int> CleanupExpiredUploadsAsync(CancellationToken cancellationToken = default)
    {
        var candidates = await _photoUploadInitTracker.GetCleanupCandidatesAsync(DateTimeOffset.UtcNow, cancellationToken);
        var cleaned = 0;

        foreach (var candidate in candidates)
        {
            try
            {
                await _photoStorageProvider.DeleteAsync(candidate.StorageKey, cancellationToken);

                await _photoUploadInitTracker.MarkExpiredAsync(candidate.StorageKey, cancellationToken);

                cleaned++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean expired photo upload session {StorageKey}", candidate.StorageKey);
            }
        }

        if (cleaned > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return cleaned;
    }
}
