using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.Application.Reporting.Contracts.BackgroundCommands;

public sealed class RecurringReportReminderInAppNotificationCommand : IActionCommand
{
    public Id<AccountReference> TraineeId { get; init; }

    public Id<AccountReference> TrainerId { get; init; }

    public Id<RecurringReportAssignment> AssignmentId { get; init; }

    public int ScheduleRevision { get; init; }

    public DateTimeOffset ScheduledFor { get; init; }

    public Id<ReportRequest>? ReportRequestId { get; init; }

    public bool IsUpcomingRequestReminder { get; init; }
}
