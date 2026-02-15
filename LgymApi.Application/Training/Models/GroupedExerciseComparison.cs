namespace LgymApi.Application.Features.Training.Models;

public sealed class GroupedExerciseComparison
{
    public Guid ExerciseId { get; init; }
    public string ExerciseName { get; init; } = string.Empty;
    public List<SeriesComparison> SeriesComparisons { get; init; } = new();
}
