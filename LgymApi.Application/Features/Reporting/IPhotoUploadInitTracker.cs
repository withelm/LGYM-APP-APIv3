using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.Reporting;

public sealed class PendingPhotoUpload
{
    public string StorageKey { get; set; } = string.Empty;
    public Id<LgymApi.Domain.Entities.User> InitiatedByUserId { get; set; }
    public Id<LgymApi.Domain.Entities.User> OwnerUserId { get; set; }
    public Id<LgymApi.Domain.Entities.ReportRequest> ReportRequestId { get; set; }
    public string ViewType { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
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

    Task<PendingPhotoUpload?> GetPendingUploadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task RemovePendingUploadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}
