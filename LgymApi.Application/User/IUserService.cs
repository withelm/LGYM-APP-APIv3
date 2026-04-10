using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.User.Models;
using LgymApi.Domain.ValueObjects;
using UserSessionEntity = LgymApi.Domain.Entities.UserSession;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.User;

public interface IUserService
{
    Task<Result<Unit, AppError>> RegisterAsync(RegisterUserInput input, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> RegisterTrainerAsync(RegisterUserInput input, CancellationToken cancellationToken = default);
    Task<Result<LoginResult, AppError>> LoginAsync(string name, string password, CancellationToken cancellationToken = default);
    Task<Result<LoginResult, AppError>> LoginTrainerAsync(string name, string password, CancellationToken cancellationToken = default);
    Task<bool> IsAdminAsync(Id<UserEntity> userId, CancellationToken cancellationToken = default);
    Task<Result<UserInfoResult, AppError>> CheckTokenAsync(UserEntity? currentUser, CancellationToken cancellationToken = default);
    Task<Result<List<RankingEntry>, AppError>> GetUsersRankingAsync(CancellationToken cancellationToken = default);
    Task<Result<int, AppError>> GetUserEloAsync(Id<UserEntity> userId, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> LogoutAsync(UserEntity? currentUser, Id<UserSessionEntity>? sessionId, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> DeleteAccountAsync(UserEntity? currentUser, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> ChangeVisibilityInRankingAsync(UserEntity? currentUser, bool isVisibleInRanking, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> UpdateTimeZoneAsync(UserEntity? currentUser, string preferredTimeZone, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> UpdateUserRolesAsync(Id<UserEntity> targetUserId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
}
