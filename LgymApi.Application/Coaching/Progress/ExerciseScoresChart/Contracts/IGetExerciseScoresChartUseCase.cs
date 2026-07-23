using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.WorkoutProgress.ProgressData.Models;

namespace LgymApi.Application.Coaching.Progress.ExerciseScoresChart;

public interface IGetExerciseScoresChartUseCase
{
    Task<Result<List<ExerciseScoreChartPoint>, AppError>> ExecuteAsync(
        GetExerciseScoresChartQuery query,
        CancellationToken cancellationToken = default);
}
