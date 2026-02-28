using LgymApi.Domain.Enums;

namespace LgymApi.BackgroundWorker.Common.Commands;

/// <summary>
/// Typed command dispatched when a training session is completed.
/// Encapsulates all data required by handlers for main-record update and email notification.
/// </summary>
public sealed class TrainingCompletedCommand : IActionCommand
{
    /// <summary>
    /// Gets the ID of the user who completed the training.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Gets the ID of the completed training session.
    /// </summary>
    public Guid TrainingId { get; init; }

    /// <summary>
    /// Gets the UTC date/time when training was created.
    /// Used for main-record synchronization timestamp.
    /// </summary>
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// Gets the collection of exercises completed in this training.
    /// Used by main-record update handler to sync best records.
    /// </summary>
    public IReadOnlyCollection<TrainingExerciseInput> Exercises { get; init; } = Array.Empty<TrainingExerciseInput>();

    /// <summary>
    /// Gets the user's email address for notification sending.
    /// </summary>
    public string RecipientEmail { get; init; } = string.Empty;

    /// <summary>
    /// Gets the user's preferred culture/language for email composition.
    /// Defaults to "en-US" if not provided.
    /// </summary>
    public string CultureName { get; init; } = "en-US";

    /// <summary>
    /// Gets the name of the plan day associated with this training (optional).
    /// Used for email template composition.
    /// </summary>
    public string? PlanDayName { get; init; }

    /// <summary>
    /// Gets the list of exercise details with names and scores for email summary.
    /// </summary>
    public IReadOnlyList<TrainingExerciseDetail> ExerciseDetails { get; init; } = Array.Empty<TrainingExerciseDetail>();
}

/// <summary>
/// Represents a single exercise input from training completion request.
/// Contains exercise ID, weight, reps, sets, and unit for record synchronization.
/// </summary>
public sealed class TrainingExerciseInput
{
    public string ExerciseId { get; init; } = string.Empty;
    public double Weight { get; init; }
    public int Reps { get; init; }
    public int Series { get; init; }
    public WeightUnits Unit { get; init; }
}

/// <summary>
/// Represents exercise details populated for email notification.
/// Contains exercise name alongside input data for display in email template.
/// </summary>
public sealed class TrainingExerciseDetail
{
    public string ExerciseName { get; init; } = string.Empty;
    public double Weight { get; init; }
    public int Reps { get; init; }
    public int Series { get; init; }
    public WeightUnits Unit { get; init; }
}
