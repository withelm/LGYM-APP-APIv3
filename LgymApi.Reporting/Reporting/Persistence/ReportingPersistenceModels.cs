using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.Application.Reporting.Persistence;

public sealed record ReportTemplateFieldPersistenceModel(
    Id<ReportTemplateField> Id,
    string Key,
    string Label,
    ReportFieldType Type,
    bool IsRequired,
    int Order,
    string? ModuleConfig,
    DateTimeOffset CreatedAt);

public sealed record ReportTemplatePersistenceModel(
    Id<ReportTemplate> Id,
    Id<AccountReference> TrainerId,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    bool IsDeleted,
    IReadOnlyList<ReportTemplateFieldPersistenceModel> Fields);

public sealed record NewReportTemplatePersistenceModel(
    Id<ReportTemplate> Id,
    Id<AccountReference> TrainerId,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ReportTemplateFieldPersistenceModel> Fields);

public sealed record UpdateReportTemplatePersistenceModel(
    string Name,
    string? Description,
    IReadOnlyList<ReportTemplateFieldPersistenceModel> Fields);

public sealed record ReportSubmissionFeedbackPersistenceModel(
    DateTimeOffset? TrainerFeedbackAddedAt,
    DateTimeOffset? TrainerFeedbackReadAt);

public sealed record ReportRequestPersistenceModel(
    Id<ReportRequest> Id,
    Id<AccountReference> TrainerId,
    Id<AccountReference> TraineeId,
    Id<ReportTemplate> TemplateId,
    Id<RecurringReportAssignment>? RecurringReportAssignmentId,
    ReportRequestStatus Status,
    DateTimeOffset? DueAt,
    DateTimeOffset? SubmittedAt,
    string? Note,
    DateTimeOffset CreatedAt,
    bool IsDeleted,
    ReportTemplatePersistenceModel Template,
    ReportSubmissionFeedbackPersistenceModel? Submission,
    ReportRequestDefinitionSnapshotPersistenceModel? DefinitionSnapshot = null,
    Id<CoachingEpisode>? SourceCoachingEpisodeId = null);

public sealed record NewReportRequestPersistenceModel(
    Id<ReportRequest> Id,
    Id<AccountReference> TrainerId,
    Id<AccountReference> TraineeId,
    Id<ReportTemplate> TemplateId,
    Id<RecurringReportAssignment>? RecurringReportAssignmentId,
    ReportRequestStatus Status,
    DateTimeOffset? DueAt,
    DateTimeOffset? SubmittedAt,
    string? Note,
    DateTimeOffset CreatedAt,
    string? DefinitionSnapshotJson = null,
    Id<CoachingEpisode>? SourceCoachingEpisodeId = null);

public sealed record ReportRequestDefinitionSnapshotPersistenceModel(
    Id<ReportRequest> ReportRequestId,
    Id<ReportTemplate> SourceTemplateId,
    string DefinitionJson,
    string Provenance,
    DateTimeOffset CreatedAt);

public sealed record ReportSubmissionPersistenceModel(
    Id<ReportSubmission> Id,
    Id<ReportRequest> ReportRequestId,
    Id<AccountReference> TraineeId,
    string PayloadJson,
    string? TrainerOverallComment,
    string? TrainerFieldCommentsJson,
    DateTimeOffset? TrainerFeedbackAddedAt,
    DateTimeOffset? TrainerFeedbackReadAt,
    DateTimeOffset CreatedAt,
    ReportRequestPersistenceModel ReportRequest,
    Id<CoachingEpisode>? SourceCoachingEpisodeId = null,
    DateTimeOffset? AcceptedAt = null);

public sealed record NewReportSubmissionPersistenceModel(
    Id<ReportSubmission> Id,
    Id<ReportRequest> ReportRequestId,
    Id<AccountReference> TraineeId,
    string PayloadJson,
    DateTimeOffset CreatedAt,
    Id<CoachingEpisode>? SourceCoachingEpisodeId = null,
    DateTimeOffset? AcceptedAt = null);

public sealed record ReportSubmissionFeedbackUpdatePersistenceModel(
    string? TrainerOverallComment,
    string? TrainerFieldCommentsJson,
    DateTimeOffset? TrainerFeedbackAddedAt,
    DateTimeOffset? TrainerFeedbackReadAt);

public sealed record RecurringReportAssignmentPersistenceModel(
    Id<RecurringReportAssignment> Id,
    Id<AccountReference> TrainerId,
    Id<AccountReference> TraineeId,
    Id<ReportTemplate> TemplateId,
    int IntervalValue,
    RecurringReportIntervalUnit IntervalUnit,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    bool IsActive,
    string? Note,
    Id<ReportRequest>? CurrentReportRequestId,
    DateTimeOffset? LastRequestCreatedAt,
    DateTimeOffset? NextEligibleAt,
    DateTimeOffset CreatedAt,
    bool IsDeleted,
    ReportTemplatePersistenceModel Template,
    ReportRequestPersistenceModel? CurrentReportRequest);

public sealed record NewRecurringReportAssignmentPersistenceModel(
    Id<RecurringReportAssignment> Id,
    Id<AccountReference> TrainerId,
    Id<AccountReference> TraineeId,
    Id<ReportTemplate> TemplateId,
    int IntervalValue,
    RecurringReportIntervalUnit IntervalUnit,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    bool IsActive,
    string? Note,
    Id<ReportRequest>? CurrentReportRequestId,
    DateTimeOffset? LastRequestCreatedAt,
    DateTimeOffset? NextEligibleAt,
    DateTimeOffset CreatedAt);

public sealed record RecurringReportAssignmentUpdatePersistenceModel(
    Id<ReportTemplate> TemplateId,
    int IntervalValue,
    RecurringReportIntervalUnit IntervalUnit,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    bool IsActive,
    string? Note,
    Id<ReportRequest>? CurrentReportRequestId,
    DateTimeOffset? LastRequestCreatedAt,
    DateTimeOffset? NextEligibleAt,
    bool IsDeleted);

public sealed record ReportPhotoPersistenceModel(
    Id<Photo> Id,
    string StorageKey,
    string MimeType,
    long SizeBytes,
    string Checksum,
    string? ThumbnailStorageKey,
    string ViewType,
    Id<ReportRequest> ReportRequestId,
    Id<AccountReference> UploaderAccountId,
    Id<AccountReference> OwnerAccountId,
    DateTimeOffset CreatedAt,
    bool IsDeleted,
    Id<TrainerPhotoType>? SourcePhotoTypeId = null,
    string? ReportPhotoSlotId = null);

public sealed record NewReportPhotoPersistenceModel(
    Id<Photo> Id,
    string StorageKey,
    string MimeType,
    long SizeBytes,
    string Checksum,
    string? ThumbnailStorageKey,
    string ViewType,
    Id<ReportRequest> ReportRequestId,
    Id<AccountReference> UploaderAccountId,
    Id<AccountReference> OwnerAccountId,
    DateTimeOffset CreatedAt,
    Id<TrainerPhotoType>? SourcePhotoTypeId = null,
    string? ReportPhotoSlotId = null);

public sealed record PendingPhotoUpload(
    Id<PhotoUploadSession> Id,
    string StorageKey,
    Id<AccountReference> InitiatedByAccountId,
    Id<AccountReference> OwnerAccountId,
    Id<ReportRequest> ReportRequestId,
    string ViewType,
    string DeclaredContentType,
    long DeclaredSizeBytes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? CompletedAtUtc,
    Id<Photo>? CompletedPhotoId,
    PhotoUploadSessionStatus Status,
    string? FailureReason,
    Id<TrainerPhotoType>? SourcePhotoTypeId = null,
    string? ReportPhotoSlotId = null);

public sealed record TrainerPhotoTypePersistenceModel(
    Id<TrainerPhotoType> Id,
    Id<AccountReference> TrainerId,
    string? SystemKey,
    string Name,
    PhotoTypeNameMode NameMode,
    bool IsArchived,
    DateTimeOffset CreatedAt);

public sealed record NewTrainerPhotoTypePersistenceModel(
    Id<TrainerPhotoType> Id,
    Id<AccountReference> TrainerId,
    string? SystemKey,
    string Name,
    PhotoTypeNameMode NameMode,
    bool IsArchived,
    DateTimeOffset CreatedAt);

public sealed record ReportingRelationshipPeriodFact(
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    CoachingEpisodeStartProvenance StartedAtProvenance = CoachingEpisodeStartProvenance.ServerAcceptedInvitation,
    Id<CoachingEpisode>? EpisodeId = null)
{
    public bool Contains(DateTimeOffset occurredAt)
        => StartedAt is not null
            && StartedAt <= occurredAt
            && (EndedAt is null || occurredAt < EndedAt);

    public bool ContainsTrustedSourceRecord(DateTimeOffset occurredAt)
        => Contains(occurredAt)
            || (StartedAt is null
                && StartedAtProvenance == CoachingEpisodeStartProvenance.ExistingActiveLinkObservedDuringMigration
                && (EndedAt is null || occurredAt < EndedAt));
}

public sealed record ReportingRelationshipAccessFact(
    bool HasActiveRelationship,
    IReadOnlyList<ReportingRelationshipPeriodFact> RelationshipPeriods,
    ReportingActiveRelationshipFact? ActiveEpisode = null)
{
    public ReportingRelationshipAccessFact(bool hasActiveRelationship)
        : this(hasActiveRelationship, [])
    {
    }

    public bool HadRelationshipAt(DateTimeOffset occurredAt)
        => RelationshipPeriods.Any(period => period.Contains(occurredAt));

    public bool CanReadTrainerAuthoredRecordAt(DateTimeOffset occurredAt)
        => HasActiveRelationship
            || RelationshipPeriods.Any(period => period.ContainsTrustedSourceRecord(occurredAt));
}

public sealed record ReportingActiveRelationshipFact(
    Id<CoachingEpisode> EpisodeId,
    long AuthorizationRevision);

internal sealed record ReportPhotoCapabilitySource(
    ReportPhotoPersistenceModel? CanonicalPhoto,
    string? ReadUrl,
    string? ThumbnailUrl);
