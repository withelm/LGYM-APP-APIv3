using System.Collections.Concurrent;
using LgymApi.Application.Features.Reporting;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Infrastructure.Services;

public sealed class InMemoryPhotoUploadInitTracker : IPhotoUploadInitTracker
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<DateTimeOffset>> _entries = new();
    private readonly ConcurrentDictionary<string, PendingPhotoUpload> _pendingUploads = new(StringComparer.Ordinal);

    public Task<int> CountRecentUploadInitsAsync(
        Id<LgymApi.Domain.Entities.User> userId,
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var queue = _entries.GetOrAdd(userId.ToString(), _ => new ConcurrentQueue<DateTimeOffset>());
        Prune(queue, sinceUtc);
        return Task.FromResult(queue.Count);
    }

    public Task RecordUploadInitAsync(
        PendingPhotoUpload pendingUpload,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var queue = _entries.GetOrAdd(pendingUpload.InitiatedByUserId.ToString(), _ => new ConcurrentQueue<DateTimeOffset>());
        queue.Enqueue(pendingUpload.CreatedAtUtc);
        Prune(queue, pendingUpload.CreatedAtUtc.AddHours(-1));
        _pendingUploads[pendingUpload.StorageKey] = pendingUpload;
        return Task.CompletedTask;
    }

    public Task<PendingPhotoUpload?> GetUploadSessionAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_pendingUploads.TryGetValue(storageKey, out var pendingUpload))
        {
            return Task.FromResult<PendingPhotoUpload?>(null);
        }

        return Task.FromResult<PendingPhotoUpload?>(pendingUpload);
    }

    public Task MarkCompletedAsync(
        string storageKey,
        Id<LgymApi.Domain.Entities.Photo> photoId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_pendingUploads.TryGetValue(storageKey, out var pendingUpload))
        {
            pendingUpload.Status = LgymApi.Domain.Enums.PhotoUploadSessionStatus.Completed;
            pendingUpload.CompletedPhotoId = photoId;
            pendingUpload.CompletedAtUtc = completedAtUtc;
        }

        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(
        string storageKey,
        string failureReason,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_pendingUploads.TryGetValue(storageKey, out var pendingUpload))
        {
            pendingUpload.Status = LgymApi.Domain.Enums.PhotoUploadSessionStatus.Failed;
            pendingUpload.FailureReason = failureReason;
        }

        return Task.CompletedTask;
    }

    public Task MarkExpiredAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_pendingUploads.TryGetValue(storageKey, out var pendingUpload))
        {
            pendingUpload.Status = LgymApi.Domain.Enums.PhotoUploadSessionStatus.Expired;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PendingPhotoUpload>> GetCleanupCandidatesAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<PendingPhotoUpload> candidates = _pendingUploads.Values
            .Where(x => x.ExpiresAtUtc < nowUtc
                        && x.Status is LgymApi.Domain.Enums.PhotoUploadSessionStatus.Pending or LgymApi.Domain.Enums.PhotoUploadSessionStatus.Failed)
            .ToList();

        return Task.FromResult(candidates);
    }

    public Task RemovePendingUploadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _pendingUploads.TryRemove(storageKey, out _);
        return Task.CompletedTask;
    }

    private static void Prune(ConcurrentQueue<DateTimeOffset> queue, DateTimeOffset sinceUtc)
    {
        while (queue.TryPeek(out var timestamp) && timestamp < sinceUtc)
        {
            queue.TryDequeue(out _);
        }
    }
}
