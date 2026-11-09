using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.Application.Reporting.Contracts.BackgroundCommands;

public sealed record ReportSubmissionAcceptedProgressPayload(
    int SchemaVersion,
    string EventId,
    string ReportSubmissionId,
    string CorrelationId,
    string CausationId,
    Id<AccountReference> TraineeId,
    DateTimeOffset ObservedAt,
    DateTimeOffset AcceptedAt,
    IReadOnlyList<ReportSubmissionAcceptedProgressMeasurement> Measurements,
    string? SourceReportRequestId = null,
    string? SourceCoachingEpisodeId = null)
{
    public const int CurrentSchemaVersion = 2;

    public ReportSubmissionAcceptedProgressPayloadValidationResult Validate()
    {
        if (SchemaVersion is not (1 or CurrentSchemaVersion))
        {
            return ReportSubmissionAcceptedProgressPayloadValidationResult.UnsupportedSchema(
                $"Schema version '{SchemaVersion}' is not supported.");
        }

        if (!IsCanonicalId(EventId)
            || !IsCanonicalId(ReportSubmissionId)
            || !IsCanonicalId(CorrelationId)
            || !IsCanonicalId(CausationId)
            || TraineeId.IsEmpty
            || ObservedAt == default
            || AcceptedAt == default
            || Measurements == null
            || Measurements.Count == 0
            || Measurements.Any(measurement => !IsValidMeasurement(measurement))
            || (SchemaVersion == CurrentSchemaVersion
                && (!IsCanonicalId(SourceReportRequestId)
                    || (SourceCoachingEpisodeId is not null && !IsCanonicalId(SourceCoachingEpisodeId)))))
        {
            return ReportSubmissionAcceptedProgressPayloadValidationResult.Invalid(
                "The accepted report submission payload contains invalid identifiers, timestamps, or measurements.");
        }

        return ReportSubmissionAcceptedProgressPayloadValidationResult.Valid();
    }

    private static bool IsCanonicalId(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && Id<ReportSubmissionAcceptedProgressPayload>.TryParse(value, out var id)
            && string.Equals(value, id.ToString(), StringComparison.Ordinal);
    }

    private static bool IsValidMeasurement(ReportSubmissionAcceptedProgressMeasurement measurement)
    {
        return measurement != null
            && measurement.BodyPart != BodyParts.Unknown
            && Enum.IsDefined(measurement.BodyPart)
            && measurement.Unit != MeasurementUnits.Unknown
            && Enum.IsDefined(measurement.Unit)
            && double.IsFinite(measurement.Value)
            && measurement.Value > 0;
    }
}

public sealed record ReportSubmissionAcceptedProgressMeasurement(
    BodyParts BodyPart,
    double Value,
    MeasurementUnits Unit);

public enum ReportSubmissionAcceptedProgressPayloadValidationOutcome
{
    Valid = 0,
    Invalid = 1,
    UnsupportedSchema = 2,
    Poison = 3
}

public sealed record ReportSubmissionAcceptedProgressPayloadValidationResult(
    ReportSubmissionAcceptedProgressPayloadValidationOutcome Outcome,
    string? Reason)
{
    public bool IsValid => Outcome == ReportSubmissionAcceptedProgressPayloadValidationOutcome.Valid;

    public static ReportSubmissionAcceptedProgressPayloadValidationResult Valid()
        => new(ReportSubmissionAcceptedProgressPayloadValidationOutcome.Valid, null);

    public static ReportSubmissionAcceptedProgressPayloadValidationResult Invalid(string reason)
        => new(ReportSubmissionAcceptedProgressPayloadValidationOutcome.Invalid, reason);

    public static ReportSubmissionAcceptedProgressPayloadValidationResult UnsupportedSchema(string reason)
        => new(ReportSubmissionAcceptedProgressPayloadValidationOutcome.UnsupportedSchema, reason);

    public static ReportSubmissionAcceptedProgressPayloadValidationResult Poison(string reason)
        => new(ReportSubmissionAcceptedProgressPayloadValidationOutcome.Poison, reason);
}
