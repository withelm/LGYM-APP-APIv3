using LgymApi.Application.Features.ExerciseScores.Models;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.ExerciseScores;

public interface IExerciseScoresService
{
    Task<List<ExerciseScoresChartData>> GetExerciseScoresChartDataAsync(Id<LgymApi.Domain.Entities.User> userId, Id<LgymApi.Domain.Entities.Exercise> exerciseId, CancellationToken cancellationToken = default);
}
