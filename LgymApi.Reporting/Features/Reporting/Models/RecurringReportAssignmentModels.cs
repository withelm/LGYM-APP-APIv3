using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.Application.Features.Reporting.Models;

public sealed class UpsertRecurringReportAssignmentCommand
{
    public Id<ReportTemplate> TemplateId { get; set; }
    public int IntervalValue { get; set; }
    public RecurringReportIntervalUnit IntervalUnit { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public string? Note { get; set; }
}

public sealed class SetNextRecurringReportOverrideCommand
{
    public DateTimeOffset ScheduledAt { get; set; }
    public int ExpectedScheduleRevision { get; set; }
}

public sealed class ProposeNextRecurringReportScheduleCommand
{
    public DateTimeOffset ScheduledAt { get; set; }
    public string? Reason { get; set; }
    public int ExpectedScheduleRevision { get; set; }
}

public sealed class RecurringReportAssignmentResult
{
    public Id<RecurringReportAssignment> Id { get; set; }
    public Id<AccountReference> TrainerId { get; set; }
    public Id<AccountReference> TraineeId { get; set; }
    public Id<ReportTemplate> TemplateId { get; set; }
    public int IntervalValue { get; set; }
    public RecurringReportIntervalUnit IntervalUnit { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public bool IsActive { get; set; }
    public string? Note { get; set; }
    public Id<ReportRequest>? CurrentReportRequestId { get; set; }
    public DateTimeOffset? LastRequestCreatedAt { get; set; }
    public DateTimeOffset? NextEligibleAt { get; set; }
    public int ScheduleRevision { get; set; }
    public NextRecurringReportRequestResult EffectiveNextRecurringRequest { get; set; } = new();
    public RecurringReportScheduleProposalResult? PendingScheduleProposal { get; set; }
    public IReadOnlyList<RecurringReportScheduleProposalResult> ScheduleProposalHistory { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public ReportTemplateResult Template { get; set; } = new();
    public ReportRequestResult? CurrentReportRequest { get; set; }
}

public sealed class RecurringReportScheduleProposalResult
{
    public Id<RecurringReportScheduleChange> Id { get; set; }
    public Id<RecurringReportAssignment> AssignmentId { get; set; }
    public DateTimeOffset OldScheduledAt { get; set; }
    public DateTimeOffset ProposedScheduledAt { get; set; }
    public string? Reason { get; set; }
    public int ScheduleRevision { get; set; }
    public RecurringReportScheduleChangeStatus Status { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public Id<AccountReference> RequestedByAccountId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Id<AccountReference>? ResolvedByAccountId { get; set; }
}
