using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class PhotoUploadSession : EntityBase<PhotoUploadSession>
{
    public string StorageKey { get; set; } = string.Empty;
    public Id<User> OwnerUserId { get; set; }
    public Id<User> InitiatedByUserId { get; set; }
    public Id<ReportRequest> ReportRequestId { get; set; }
    public string ViewType { get; set; } = string.Empty;
    public string DeclaredContentType { get; set; } = string.Empty;
    public long DeclaredSizeBytes { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public Id<Photo>? CompletedPhotoId { get; set; }
    public PhotoUploadSessionStatus Status { get; set; } = PhotoUploadSessionStatus.Pending;
    public string? FailureReason { get; set; }

    public ReportRequest ReportRequest { get; set; } = null!;
    public User OwnerUser { get; set; } = null!;
    public User InitiatedByUser { get; set; } = null!;
    public Photo? CompletedPhoto { get; set; }
}
