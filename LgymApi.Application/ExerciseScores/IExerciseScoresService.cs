using LgymApi.Application.Features.ExerciseScores.Models;

namespace LgymApi.Application.Features.ExerciseScores;

public interface IExerciseScoresService
{
    Task<List<ExerciseScoresChartData>> GetExerciseScoresChartDataAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default);
}
