using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.ExerciseScores.Models;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.ExerciseScores;

public interface IExerciseScoresService
{
    Task<Result<List<ExerciseScoresChartData>, AppError>> GetExerciseScoresChartDataAsync(Id<LgymApi.Domain.Entities.User> userId, Id<LgymApi.Domain.Entities.Exercise> exerciseId, CancellationToken cancellationToken = default);
}
