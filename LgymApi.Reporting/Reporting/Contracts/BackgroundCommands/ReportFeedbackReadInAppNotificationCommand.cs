using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.Application.Reporting.Contracts.BackgroundCommands;

public sealed class ReportFeedbackReadInAppNotificationCommand : IActionCommand
{
    public Id<ReportSubmission> SubmissionId { get; init; }
    public Id<AccountReference> TraineeId { get; init; }
    public Id<AccountReference> TrainerId { get; init; }
    public DateTimeOffset TriggeredAt { get; init; }
    public DateTimeOffset? NextRecurringRequestAt { get; init; }
}
