using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using ExerciseScoreEntity = LgymApi.Domain.Entities.ExerciseScore;

namespace LgymApi.Application.Features.Training.Models;

public sealed class EnrichedExercise
{
    public Guid ExerciseScoreId { get; init; }
    public ExerciseEntity ExerciseDetails { get; init; } = new();
    public List<ExerciseScoreEntity> ScoresDetails { get; init; } = new();
}
