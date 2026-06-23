using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class RecurringReportAssignment : EntityBase<RecurringReportAssignment>
{
    public Id<User> TrainerId { get; set; }
    public Id<User> TraineeId { get; set; }
    public Id<ReportTemplate> TemplateId { get; set; }
    public int IntervalValue { get; set; }
    public RecurringReportIntervalUnit IntervalUnit { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }
    public Id<ReportRequest>? CurrentReportRequestId { get; set; }
    public DateTimeOffset? LastRequestCreatedAt { get; set; }
    public DateTimeOffset? NextEligibleAt { get; set; }

    public User Trainer { get; set; } = null!;
    public User Trainee { get; set; } = null!;
    public ReportTemplate Template { get; set; } = null!;
    public ReportRequest? CurrentReportRequest { get; set; }
}
