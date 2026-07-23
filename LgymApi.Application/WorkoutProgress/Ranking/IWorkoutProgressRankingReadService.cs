using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.WorkoutProgress.Ranking.Models;

namespace LgymApi.Application.WorkoutProgress.Ranking;

public interface IWorkoutProgressRankingReadService
{
    Task<Result<List<RankingReadModel>, AppError>> GetUsersRankingAsync(CancellationToken cancellationToken = default);
}
