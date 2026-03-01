using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Domain.Enums;

namespace LgymApi.BackgroundWorker.Common.Commands;

/// <summary>
/// Command dispatched when a training session is completed.
/// Triggers both main-record update and training completed email sending.
/// </summary>
public sealed class TrainingCompletedCommand : IActionCommand
{
    public Guid UserId { get; init; }
    public Guid TrainingId { get; init; }
    public string RecipientEmail { get; init; } = string.Empty;
    public string CultureName { get; init; } = "en-US";
    public string PlanDayName { get; init; } = string.Empty;
    public DateTimeOffset TrainingDate { get; init; }
    public IReadOnlyList<TrainingExerciseSummary> Exercises { get; init; } = Array.Empty<TrainingExerciseSummary>();
}
