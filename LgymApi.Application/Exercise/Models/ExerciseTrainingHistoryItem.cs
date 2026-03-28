namespace LgymApi.Application.Features.Exercise.Models;

public sealed class ExerciseTrainingHistoryItem
{
    public LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training> Id { get; init; }
    public DateTime Date { get; init; }
    public string GymName { get; init; } = string.Empty;
    public string TrainingName { get; init; } = string.Empty;
    public List<SeriesScoreResult> SeriesScores { get; init; } = new();
}
