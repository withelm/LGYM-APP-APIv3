using System.Text.Json.Serialization;

namespace LgymApi.Infrastructure.Reporting.PhotoTypesMigration;

public sealed class ReportingPhotoTypesMigrationReport
{
    public required string Mode { get; init; }
    public required string Fingerprint { get; init; }
    public required string Provenance { get; init; }
    public required DateTimeOffset CapturedAtUtc { get; init; }
    public bool Applied { get; set; }
    public int BatchSize { get; init; }
    public ReportingPhotoTypesMigrationCounters Counters { get; init; } = new();
    public List<ReportingPhotoTypesMigrationIssue> Exceptions { get; init; } = [];

    [JsonIgnore]
    public bool HasGlobalBlockers => Exceptions.Any(issue => issue.Scope == "Global" && issue.Severity == "Error");

    [JsonIgnore]
    public bool HasUnresolvedRequests => Exceptions.Any(issue => issue.Scope == "Request" && issue.Severity == "Error");
}

public sealed class ReportingPhotoTypesMigrationCounters
{
    public int RequestsExamined { get; set; }
    public int RequestsAlreadySnapshotted { get; set; }
    public int RequestsExcludedDeleted { get; set; }
    public int RequestsPlanned { get; set; }
    public int RequestsUnsafe { get; set; }
    public int SnapshotsWritten { get; set; }
    public int ExistingSnapshotsPreserved { get; set; }
    public int DefaultsPlanned { get; set; }
    public int DefaultsWritten { get; set; }
    public int ExistingDefaultsPreserved { get; set; }
    public int PhotosExamined { get; set; }
    public int PhotoMappingsPlanned { get; set; }
    public int PhotosMapped { get; set; }
    public int PhotosRetainedRaw { get; set; }
    public int PhotosExcludedDeleted { get; set; }
    public int PendingUploadSessions { get; set; }
    public int BatchesCommitted { get; set; }
}

public sealed record ReportingPhotoTypesMigrationIssue(
    string Severity,
    string Scope,
    string Code,
    string EntityId,
    string Detail);
