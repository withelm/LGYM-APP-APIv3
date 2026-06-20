using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.BackgroundWorker.Common.Commands;

/// <summary>
/// Dispatched when a trainer adds feedback to a submitted report.
/// Triggers in-app notification creation for the trainee.
/// </summary>
public sealed class ReportFeedbackAddedInAppNotificationCommand : IActionCommand
{
    public Id<ReportSubmission> SubmissionId { get; init; }
    public Id<User> TraineeId { get; init; }
    public Id<User> TrainerId { get; init; }
    public string TemplateName { get; init; } = string.Empty;
    public DateTimeOffset TriggeredAt { get; init; }
}
