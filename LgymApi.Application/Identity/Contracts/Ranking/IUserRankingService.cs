using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.User.Models;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Identity.Contracts.Ranking;

public interface IUserRankingService
{
    Task<Result<List<RankingEntry>, AppError>> GetUsersRankingAsync(CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> ChangeVisibilityInRankingAsync(
        UserEntity? currentUser,
        bool isVisibleInRanking,
        CancellationToken cancellationToken = default);
}
