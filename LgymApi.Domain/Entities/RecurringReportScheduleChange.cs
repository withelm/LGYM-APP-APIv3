using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class RecurringReportScheduleChange : EntityBase<RecurringReportScheduleChange>
{
    public Id<RecurringReportAssignment> RecurringReportAssignmentId { get; set; }
    public RecurringReportScheduleChangeKind Kind { get; set; }
    public RecurringReportScheduleChangeStatus Status { get; set; }
    public int ScheduleRevision { get; set; }
    public DateTimeOffset OldScheduledAt { get; set; }
    public DateTimeOffset ProposedScheduledAt { get; set; }
    public string? Reason { get; set; }
    public Id<User> RequestedByUserId { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public Id<User>? ResolvedByUserId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Id<RecurringReportScheduleChange>? SourceProposalId { get; set; }

    public RecurringReportAssignment RecurringReportAssignment { get; set; } = null!;
    public User RequestedByUser { get; set; } = null!;
    public User? ResolvedByUser { get; set; }
    public RecurringReportScheduleChange? SourceProposal { get; set; }
}
