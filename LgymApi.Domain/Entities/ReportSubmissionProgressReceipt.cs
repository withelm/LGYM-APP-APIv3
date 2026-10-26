using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class ReportSubmissionProgressReceipt : EntityBase<ReportSubmissionProgressReceipt>
{
    public Id<ReportSubmission> ReportSubmissionId { get; set; }
}
