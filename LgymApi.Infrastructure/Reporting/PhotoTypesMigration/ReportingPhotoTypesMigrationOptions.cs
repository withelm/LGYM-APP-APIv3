namespace LgymApi.Infrastructure.Reporting.PhotoTypesMigration;

public enum ReportingPhotoTypesMigrationMode
{
    DryRun,
    Apply
}

public sealed record ReportingPhotoTypesMigrationOptions(
    ReportingPhotoTypesMigrationMode Mode,
    int BatchSize,
    string? ExpectedFingerprint,
    bool AllowRetainedDeletedOwners,
    bool ExternalFenceAcknowledged,
    bool IgnoreExpiredPendingUploads = false);
