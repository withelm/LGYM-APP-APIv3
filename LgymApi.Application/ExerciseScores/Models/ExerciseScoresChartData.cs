namespace LgymApi.Application.Features.ExerciseScores.Models;

public sealed class ExerciseScoresChartData
{
    public string Id { get; init; } = string.Empty;
    public double Value { get; init; }
    public string Date { get; init; } = string.Empty;
    public string ExerciseName { get; init; } = string.Empty;
    public string ExerciseId { get; init; } = string.Empty;
}
