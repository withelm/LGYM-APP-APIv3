using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Infrastructure.Data;
using LgymApi.Reporting.AccountDeletion;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories.Reporting;

public sealed class PhotoDeletionPersistence(AppDbContext database) : IPhotoDeletionPersistence
{
    public async Task StageAccountAsync(Id<AccountReference> accountId, Id<CorrelationScope> generation, CancellationToken cancellationToken)
    {
        var userId = ReportingPersistenceAccountIds.ToPersisted(accountId);
        if (!await database.Users.IgnoreQueryFilters().AnyAsync(user => user.Id == userId
            && user.DeletionState == AccountDeletionState.Finalizing && user.DeletionGeneration == generation, cancellationToken))
        {
            throw new InvalidOperationException("Photo deletion requires the claimed finalization generation.");
        }
        var templateIds = database.ReportTemplates.IgnoreQueryFilters().Where(template => template.TrainerId == userId).Select(template => template.Id);
        var assignmentIds = database.RecurringReportAssignments.IgnoreQueryFilters()
            .Where(assignment => assignment.TrainerId == userId || assignment.TraineeId == userId || templateIds.Contains(assignment.TemplateId))
            .Select(assignment => assignment.Id);
        var requestIds = database.ReportRequests.IgnoreQueryFilters()
            .Where(request => request.TraineeId == userId || request.TrainerId == userId || templateIds.Contains(request.TemplateId)
                || (request.RecurringReportAssignmentId != null && assignmentIds.Contains(request.RecurringReportAssignmentId.Value)))
            .Select(request => request.Id);
        var photos = await database.Photos.IgnoreQueryFilters().AsNoTracking()
            .Where(photo => photo.OwnerUserId == userId || photo.UploaderUserId == userId || requestIds.Contains(photo.ReportRequestId))
            .Select(photo => new { photo.StorageKey, photo.ThumbnailStorageKey })
            .ToListAsync(cancellationToken);
        var uploads = await database.PhotoUploadSessions.IgnoreQueryFilters().AsNoTracking()
            .Where(upload => upload.OwnerUserId == userId || upload.InitiatedByUserId == userId || requestIds.Contains(upload.ReportRequestId))
            .Select(upload => new { upload.StorageKey, upload.ExpiresAtUtc })
            .ToListAsync(cancellationToken);
        var prefixes = await database.ReportRequests.IgnoreQueryFilters().AsNoTracking()
            .Where(request => requestIds.Contains(request.Id))
            .Select(request => new { request.Id, request.TraineeId })
            .ToListAsync(cancellationToken);
        var existing = await database.PhotoDeletionWorkItems.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.DeletionGeneration == generation)
            .Select(item => item.StorageKey).ToListAsync(cancellationToken);
        var known = existing.ToHashSet(StringComparer.Ordinal);
        // Defer any historical upload capability whose recorded expiry extends past finalization.
        var notBefore = uploads.Count == 0 ? DateTimeOffset.MinValue : uploads.Max(upload => upload.ExpiresAtUtc);
        foreach (var photo in photos)
        {
            StageKey(photo.StorageKey, false);
            if (!string.IsNullOrWhiteSpace(photo.ThumbnailStorageKey)) StageKey(photo.ThumbnailStorageKey, false);
        }
        foreach (var upload in uploads) StageKey(upload.StorageKey, false);
        StageKey($"photos/{userId}/", true);
        foreach (var request in prefixes) StageKey($"photos/{request.TraineeId}/{request.Id}/", true);

        void StageKey(string key, bool prefix)
        {
            if (known.Add(key))
            {
                database.PhotoDeletionWorkItems.Add(new PhotoDeletionWorkItem
                {
                    Id = Id<PhotoDeletionWorkItem>.New(), DeletionGeneration = generation,
                    StorageKey = key, IsPrefix = prefix, NotBeforeUtc = notBefore
                });
            }
        }
    }

    public async Task<IReadOnlyList<PhotoDeletionCandidate>> GetPendingAsync(DateTimeOffset now, CancellationToken cancellationToken)
        => await database.PhotoDeletionWorkItems.AsNoTracking()
            .Where(item => item.CompletedAtUtc == null && item.NotBeforeUtc <= now)
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id).Take(100)
            .Select(item => new PhotoDeletionCandidate(item.Id, item.DeletionGeneration, item.StorageKey, item.IsPrefix, item.ContinuationToken))
            .ToListAsync(cancellationToken);

    public async Task StagePageAsync(PhotoDeletionCandidate prefix, PhotoStoragePage page, CancellationToken cancellationToken)
    {
        var known = await database.PhotoDeletionWorkItems.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.DeletionGeneration == prefix.Generation && page.Keys.Contains(item.StorageKey) && !item.IsPrefix)
            .Select(item => item.StorageKey).ToListAsync(cancellationToken);
        foreach (var key in page.Keys.Except(known, StringComparer.Ordinal))
        {
            database.PhotoDeletionWorkItems.Add(new PhotoDeletionWorkItem
            {
                Id = Id<PhotoDeletionWorkItem>.New(), DeletionGeneration = prefix.Generation, StorageKey = key
            });
        }
        var item = await database.PhotoDeletionWorkItems.SingleAsync(item => item.Id == prefix.Id, cancellationToken);
        item.ContinuationToken = page.ContinuationToken;
    }

    public async Task CompleteAsync(Id<PhotoDeletionWorkItem> id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var item = await database.PhotoDeletionWorkItems.SingleAsync(item => item.Id == id, cancellationToken);
        item.CompletedAtUtc = now;
    }

    public async Task DeferAsync(Id<PhotoDeletionWorkItem> id, DateTimeOffset retryAt, CancellationToken cancellationToken)
    {
        var item = await database.PhotoDeletionWorkItems.SingleAsync(item => item.Id == id, cancellationToken);
        item.NotBeforeUtc = retryAt;
    }
}
