using LgymApi.Domain.ValueObjects;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Reporting;

public sealed class PendingPhotoUpload
{
    public Id<LgymApi.Domain.Entities.PhotoUploadSession> Id { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public Id<LgymApi.Domain.Entities.User> InitiatedByUserId { get; set; }
    public Id<LgymApi.Domain.Entities.User> OwnerUserId { get; set; }
    public Id<LgymApi.Domain.Entities.ReportRequest> ReportRequestId { get; set; }
    public PhotoViewType ViewType { get; set; }
    public string DeclaredContentType { get; set; } = string.Empty;
    public long DeclaredSizeBytes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public Id<LgymApi.Domain.Entities.Photo>? CompletedPhotoId { get; set; }
    public PhotoUploadSessionStatus Status { get; set; } = PhotoUploadSessionStatus.Pending;
    public string? FailureReason { get; set; }
}

public interface IPhotoUploadInitTracker
{
    Task<int> CountRecentUploadInitsAsync(
        Id<LgymApi.Domain.Entities.User> userId,
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken = default);

    Task RecordUploadInitAsync(
        PendingPhotoUpload pendingUpload,
        CancellationToken cancellationToken = default);

    Task<PendingPhotoUpload?> GetUploadSessionAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(
        string storageKey,
        Id<LgymApi.Domain.Entities.Photo> photoId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        string storageKey,
        string failureReason,
        CancellationToken cancellationToken = default);

    Task MarkExpiredAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingPhotoUpload>> GetCleanupCandidatesAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task RemovePendingUploadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}
