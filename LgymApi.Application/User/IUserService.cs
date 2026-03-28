using LgymApi.Application.Features.User.Models;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.User;

public interface IUserService
{
    Task RegisterAsync(RegisterUserInput input, CancellationToken cancellationToken = default);
    Task RegisterTrainerAsync(RegisterUserInput input, CancellationToken cancellationToken = default);
    Task<LoginResult> LoginAsync(string name, string password, CancellationToken cancellationToken = default);
    Task<LoginResult> LoginTrainerAsync(string name, string password, CancellationToken cancellationToken = default);
    Task<bool> IsAdminAsync(Id<UserEntity> userId, CancellationToken cancellationToken = default);
    Task<UserInfoResult> CheckTokenAsync(UserEntity currentUser, CancellationToken cancellationToken = default);
    Task<List<RankingEntry>> GetUsersRankingAsync(CancellationToken cancellationToken = default);
    Task<int> GetUserEloAsync(Id<UserEntity> userId, CancellationToken cancellationToken = default);
    Task LogoutAsync(UserEntity currentUser, CancellationToken cancellationToken = default);
    Task DeleteAccountAsync(UserEntity currentUser, CancellationToken cancellationToken = default);
    Task ChangeVisibilityInRankingAsync(UserEntity currentUser, bool isVisibleInRanking, CancellationToken cancellationToken = default);
    Task UpdateTimeZoneAsync(UserEntity currentUser, string preferredTimeZone, CancellationToken cancellationToken = default);
    Task UpdateUserRolesAsync(Id<UserEntity> targetUserId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
}
