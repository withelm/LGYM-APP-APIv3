using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.BackgroundWorker.Common.Commands;

/// <summary>
/// Dispatched when a trainee submits a report request.
/// Triggers an in-app notification for the trainer.
/// </summary>
public sealed class ReportSubmissionCreatedInAppNotificationCommand : IActionCommand
{
    public Id<ReportSubmission> SubmissionId { get; init; }
    public Id<User> TrainerId { get; init; }
    public Id<User> TraineeId { get; init; }
    public string TemplateName { get; init; } = string.Empty;
}
