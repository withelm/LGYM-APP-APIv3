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

    public Task<PendingPhotoUpload?> GetPendingUploadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_pendingUploads.TryGetValue(storageKey, out var pendingUpload))
        {
            return Task.FromResult<PendingPhotoUpload?>(null);
        }

        if (pendingUpload.ExpiresAtUtc < DateTimeOffset.UtcNow)
        {
            _pendingUploads.TryRemove(storageKey, out _);
            return Task.FromResult<PendingPhotoUpload?>(null);
        }

        return Task.FromResult<PendingPhotoUpload?>(pendingUpload);
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
