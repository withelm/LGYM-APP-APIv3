namespace LgymApi.Application.Features.Exercise.Models;

public sealed class LastExerciseScoresResult
{
    public LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Exercise> ExerciseId { get; init; }
    public string ExerciseName { get; init; } = string.Empty;
    public List<SeriesScoreResult> SeriesScores { get; init; } = new();
}
