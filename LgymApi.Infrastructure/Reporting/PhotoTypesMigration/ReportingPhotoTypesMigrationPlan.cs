using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Infrastructure.Reporting.PhotoTypesMigration;

internal sealed record ReportingPhotoTypesMigrationPlan(
    string Fingerprint,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<PlannedPhotoType> PhotoTypes,
    IReadOnlyList<PlannedRequestMigration> Requests,
    ReportingPhotoTypesMigrationReport Report);

internal sealed record PlannedPhotoType(TrainerPhotoType Entity);

internal sealed record PlannedRequestMigration(
    Id<ReportRequest> ReportRequestId,
    ReportRequestDefinitionSnapshot Snapshot,
    IReadOnlyList<PlannedPhotoBinding> PhotoBindings);

internal sealed record PlannedPhotoBinding(
    Id<Photo> PhotoId,
    Id<TrainerPhotoType> SourcePhotoTypeId,
    string ReportPhotoSlotId);
