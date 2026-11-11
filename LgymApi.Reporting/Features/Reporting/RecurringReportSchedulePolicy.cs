using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Reporting;

internal static class RecurringReportSchedulePolicy
{
    public static NextRecurringReportRequestResult EffectiveSchedule(RecurringReportAssignmentPersistenceModel assignment)
    {
        if (!assignment.IsActive || assignment.IsDeleted)
        {
            return new NextRecurringReportRequestResult { Status = NextRecurringReportRequestStatus.Unscheduled };
        }

        var activeOverride = ActiveOverride(assignment);
        var scheduledAt = activeOverride?.ProposedScheduledAt ?? BaseScheduledAt(assignment);
        if (!scheduledAt.HasValue || (assignment.EndsAt.HasValue && scheduledAt.Value > assignment.EndsAt.Value))
        {
            return new NextRecurringReportRequestResult
            {
                AssignmentId = assignment.Id,
                Status = NextRecurringReportRequestStatus.Unscheduled
            };
        }

        return new NextRecurringReportRequestResult
        {
            AssignmentId = assignment.Id,
            ScheduledAt = scheduledAt.Value,
            Status = NextRecurringReportRequestStatus.Scheduled
        };
    }

    public static RecurringReportScheduleProposalResult? PendingProposal(RecurringReportAssignmentPersistenceModel assignment)
    {
        var proposal = (assignment.ScheduleChanges ?? []).SingleOrDefault(change =>
            change.Kind == RecurringReportScheduleChangeKind.TraineeProposal
            && change.Status == RecurringReportScheduleChangeStatus.Pending);
        return proposal is null
            ? null
            : new RecurringReportScheduleProposalResult
            {
                Id = proposal.Id,
                AssignmentId = proposal.AssignmentId,
                OldScheduledAt = proposal.OldScheduledAt,
                ProposedScheduledAt = proposal.ProposedScheduledAt,
                Reason = proposal.Reason,
                ScheduleRevision = proposal.ScheduleRevision,
                Status = proposal.Status,
                RequestedAt = proposal.RequestedAt,
                RequestedByAccountId = proposal.RequestedByAccountId,
                ResolvedAt = proposal.ResolvedAt,
                ResolvedByAccountId = proposal.ResolvedByAccountId
            };
    }

    public static RecurringReportScheduleChangePersistenceModel? ActiveOverride(RecurringReportAssignmentPersistenceModel assignment)
        => (assignment.ScheduleChanges ?? []).SingleOrDefault(change =>
            change.Kind == RecurringReportScheduleChangeKind.TrainerOverride
            && change.Status == RecurringReportScheduleChangeStatus.Active);

    public static DateTimeOffset? BaseScheduledAt(RecurringReportAssignmentPersistenceModel assignment)
    {
        if (assignment.NextEligibleAt.HasValue)
        {
            return assignment.NextEligibleAt.Value;
        }

        if (assignment.CurrentReportRequest?.Submission?.TrainerFeedbackReadAt is { } readAt)
        {
            return AddInterval(readAt, assignment.IntervalValue, assignment.IntervalUnit);
        }

        return assignment.CurrentReportRequestId.HasValue ? null : assignment.StartsAt;
    }

    public static DateTimeOffset AddInterval(DateTimeOffset value, int intervalValue, RecurringReportIntervalUnit intervalUnit)
        => intervalUnit switch
        {
            RecurringReportIntervalUnit.Day => value.AddDays(intervalValue),
            RecurringReportIntervalUnit.Week => value.AddDays(intervalValue * 7d),
            RecurringReportIntervalUnit.Month => value.AddMonths(intervalValue),
            _ => value.AddDays(intervalValue)
        };
}
