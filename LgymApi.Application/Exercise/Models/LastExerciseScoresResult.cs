namespace LgymApi.Application.Features.Exercise.Models;

public sealed class LastExerciseScoresResult
{
    public Guid ExerciseId { get; init; }
    public string ExerciseName { get; init; } = string.Empty;
    public List<SeriesScoreResult> SeriesScores { get; init; } = new();
}
