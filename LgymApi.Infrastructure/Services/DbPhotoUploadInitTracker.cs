using LgymApi.Application.Features.Reporting;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Services;

public sealed class DbPhotoUploadInitTracker : IPhotoUploadInitTracker
{
    private readonly AppDbContext _dbContext;

    public DbPhotoUploadInitTracker(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> CountRecentUploadInitsAsync(
        Id<User> userId,
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.PhotoUploadSessions
            .CountAsync(x => x.InitiatedByUserId == userId && x.CreatedAt >= sinceUtc, cancellationToken);
    }

    public async Task RecordUploadInitAsync(
        PendingPhotoUpload pendingUpload,
        CancellationToken cancellationToken = default)
    {
        var entity = new PhotoUploadSession
        {
            Id = pendingUpload.Id.IsEmpty ? Id<PhotoUploadSession>.New() : pendingUpload.Id,
            StorageKey = pendingUpload.StorageKey,
            InitiatedByUserId = pendingUpload.InitiatedByUserId,
            OwnerUserId = pendingUpload.OwnerUserId,
            ReportRequestId = pendingUpload.ReportRequestId,
            ViewType = pendingUpload.ViewType,
            DeclaredContentType = pendingUpload.DeclaredContentType,
            DeclaredSizeBytes = pendingUpload.DeclaredSizeBytes,
            CreatedAt = pendingUpload.CreatedAtUtc,
            ExpiresAtUtc = pendingUpload.ExpiresAtUtc,
            CompletedAtUtc = pendingUpload.CompletedAtUtc,
            CompletedPhotoId = pendingUpload.CompletedPhotoId,
            Status = pendingUpload.Status,
            FailureReason = pendingUpload.FailureReason
        };

        await _dbContext.PhotoUploadSessions.AddAsync(entity, cancellationToken);
    }

    public async Task<PendingPhotoUpload?> GetUploadSessionAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.PhotoUploadSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.StorageKey == storageKey, cancellationToken);

        return entity == null ? null : Map(entity);
    }

    public async Task MarkCompletedAsync(
        string storageKey,
        Id<Photo> photoId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.PhotoUploadSessions
            .FirstOrDefaultAsync(x => x.StorageKey == storageKey, cancellationToken);

        if (entity == null)
        {
            return;
        }

        entity.Status = PhotoUploadSessionStatus.Completed;
        entity.CompletedPhotoId = photoId;
        entity.CompletedAtUtc = completedAtUtc;
        entity.FailureReason = null;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public async Task MarkFailedAsync(
        string storageKey,
        string failureReason,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.PhotoUploadSessions
            .FirstOrDefaultAsync(x => x.StorageKey == storageKey, cancellationToken);

        if (entity == null)
        {
            return;
        }

        entity.Status = PhotoUploadSessionStatus.Failed;
        entity.FailureReason = failureReason;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public async Task MarkExpiredAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.PhotoUploadSessions
            .FirstOrDefaultAsync(x => x.StorageKey == storageKey, cancellationToken);

        if (entity == null)
        {
            return;
        }

        entity.Status = PhotoUploadSessionStatus.Expired;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public async Task<IReadOnlyList<PendingPhotoUpload>> GetCleanupCandidatesAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.PhotoUploadSessions
            .AsNoTracking()
            .Where(x => x.ExpiresAtUtc < nowUtc
                        && (x.Status == PhotoUploadSessionStatus.Pending || x.Status == PhotoUploadSessionStatus.Failed))
            .OrderBy(x => x.ExpiresAtUtc)
            .ToListAsync(cancellationToken);

        return entities.Select(Map).ToList();
    }

    public async Task RemovePendingUploadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.PhotoUploadSessions
            .FirstOrDefaultAsync(x => x.StorageKey == storageKey, cancellationToken);

        if (entity == null)
        {
            return;
        }

        entity.IsDeleted = true;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static PendingPhotoUpload Map(PhotoUploadSession entity)
        => new()
        {
            Id = entity.Id,
            StorageKey = entity.StorageKey,
            InitiatedByUserId = entity.InitiatedByUserId,
            OwnerUserId = entity.OwnerUserId,
            ReportRequestId = entity.ReportRequestId,
            ViewType = entity.ViewType,
            DeclaredContentType = entity.DeclaredContentType,
            DeclaredSizeBytes = entity.DeclaredSizeBytes,
            CreatedAtUtc = entity.CreatedAt,
            ExpiresAtUtc = entity.ExpiresAtUtc,
            CompletedAtUtc = entity.CompletedAtUtc,
            CompletedPhotoId = entity.CompletedPhotoId,
            Status = entity.Status,
            FailureReason = entity.FailureReason
        };
}
