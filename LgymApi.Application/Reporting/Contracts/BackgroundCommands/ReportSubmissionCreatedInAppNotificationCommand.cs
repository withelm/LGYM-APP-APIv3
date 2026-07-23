using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Reporting.Contracts.BackgroundCommands;

public sealed class ReportSubmissionCreatedInAppNotificationCommand : IActionCommand
{
    public Id<ReportSubmission> SubmissionId { get; init; }

    public Id<User> TrainerId { get; init; }

    public Id<User> TraineeId { get; init; }

    public string TemplateName { get; init; } = string.Empty;
}
