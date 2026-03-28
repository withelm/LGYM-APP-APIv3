namespace LgymApi.Application.Features.Training.Models;

public sealed class GroupedExerciseComparison
{
    public LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Exercise> ExerciseId { get; init; }
    public string ExerciseName { get; init; } = string.Empty;
    public List<SeriesComparison> SeriesComparisons { get; init; } = new();
}
