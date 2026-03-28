using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class ReportRequest : EntityBase<ReportRequest>
{
    public Id<User> TrainerId { get; set; }
    public Id<User> TraineeId { get; set; }
    public Id<ReportTemplate> TemplateId { get; set; }
    public ReportRequestStatus Status { get; set; } = ReportRequestStatus.Pending;
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public string? Note { get; set; }

    public User Trainer { get; set; } = null!;
    public User Trainee { get; set; } = null!;
    public ReportTemplate Template { get; set; } = null!;
    public ReportSubmission? Submission { get; set; }
}
