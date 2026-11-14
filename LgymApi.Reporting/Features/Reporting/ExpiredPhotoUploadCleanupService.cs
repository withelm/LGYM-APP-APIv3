using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Repositories;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Enums;
using Microsoft.Extensions.Logging;
using LgymApi.Identity.Contracts.AccountDeletion;

namespace LgymApi.Application.Features.Reporting;

public sealed class ExpiredPhotoUploadCleanupService : IExpiredPhotoUploadCleanupService
{
    private readonly IReportPhotoPersistence _photoPersistence;
    private readonly IPhotoStorageProvider _photoStorageProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ExpiredPhotoUploadCleanupService> _logger;
    private readonly IAccountDeletionSynchronization _synchronization;
    private readonly IAccountDeletionRetention _retention;

    public ExpiredPhotoUploadCleanupService(
        IReportPhotoPersistence photoPersistence,
        IPhotoStorageProvider photoStorageProvider,
        IUnitOfWork unitOfWork,
        ILogger<ExpiredPhotoUploadCleanupService> logger,
        IAccountDeletionSynchronization synchronization,
        IAccountDeletionRetention retention)
    {
        _photoPersistence = photoPersistence;
        _photoStorageProvider = photoStorageProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _synchronization = synchronization;
        _retention = retention;
    }

    public async Task<int> CleanupExpiredUploadsAsync(CancellationToken cancellationToken = default)
    {
        var candidates = await _photoPersistence.ListCleanupCandidatesAsync(DateTimeOffset.UtcNow, cancellationToken);
        var cleaned = 0;

        foreach (var candidate in candidates)
        {
            var storageCompleted = false;
            try
            {
                await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
                var accounts = new[] { candidate.OwnerAccountId, candidate.InitiatedByAccountId }.Distinct().Order().ToArray();
                foreach (var account in accounts) await _synchronization.AcquireAsync(account, cancellationToken);
                await _retention.EnsureEligibleAsync(accounts, cancellationToken);
                await _photoStorageProvider.DeleteAsync(candidate.StorageKey, cancellationToken);
                storageCompleted = true;
                await _photoPersistence.MarkUploadExpiredAsync(candidate.StorageKey, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                cleaned++;
            }
            catch (Exception ex) when (!storageCompleted && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Failed to clean expired photo upload session {StorageKey}", candidate.StorageKey);
            }
        }

        return cleaned;
    }
}
