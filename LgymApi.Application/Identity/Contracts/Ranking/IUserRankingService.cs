using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Identity.Contracts.Ranking;

public interface IUserRankingService
{
    Task<Result<Unit, AppError>> ChangeVisibilityInRankingAsync(
        UserEntity? currentUser,
        bool isVisibleInRanking,
        CancellationToken cancellationToken = default);
}
