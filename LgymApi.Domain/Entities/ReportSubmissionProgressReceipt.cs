using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class ReportSubmissionProgressReceipt : EntityBase<ReportSubmissionProgressReceipt>
{
    public Id<ReportSubmission> ReportSubmissionId { get; set; }
    public Id<ReportRequest>? SourceReportRequestId { get; set; }
    public Id<CoachingEpisode>? SourceCoachingEpisodeId { get; set; }
    public DateTimeOffset? SourceReportAcceptedAt { get; set; }
}
