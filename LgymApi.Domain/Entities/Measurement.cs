using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class Measurement : EntityBase<Measurement>
{
    public Id<User> UserId { get; set; }
    public BodyParts BodyPart { get; set; }
    public string Unit { get; set; } = string.Empty;
    public double Value { get; set; }
    public Id<ReportSubmission>? SourceReportSubmissionId { get; set; }
    public Id<ReportRequest>? SourceReportRequestId { get; set; }
    public Id<CoachingEpisode>? SourceCoachingEpisodeId { get; set; }
    public DateTimeOffset? SourceReportAcceptedAt { get; set; }
    public DateTimeOffset? RecordedAt { get; set; }

    public User? User { get; set; }
}
