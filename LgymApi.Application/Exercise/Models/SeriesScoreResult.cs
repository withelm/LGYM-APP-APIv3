using ExerciseScoreEntity = LgymApi.Domain.Entities.ExerciseScore;

namespace LgymApi.Application.Features.Exercise.Models;

public sealed class SeriesScoreResult
{
    public int Series { get; init; }
    public ExerciseScoreEntity? Score { get; init; }
}
