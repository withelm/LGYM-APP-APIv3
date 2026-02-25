using LgymApi.Domain.Enums;

namespace LgymApi.Domain.Entities;

public sealed class ReportRequest : EntityBase
{
    public Guid TrainerId { get; set; }
    public Guid TraineeId { get; set; }
    public Guid TemplateId { get; set; }
    public ReportRequestStatus Status { get; set; } = ReportRequestStatus.Pending;
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public string? Note { get; set; }

    public User Trainer { get; set; } = null!;
    public User Trainee { get; set; } = null!;
    public ReportTemplate Template { get; set; } = null!;
    public ReportSubmission? Submission { get; set; }
}
