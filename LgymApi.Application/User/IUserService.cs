using LgymApi.Application.Features.User.Models;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.User;

public interface IUserService
{
    Task RegisterAsync(string name, string email, string password, string confirmPassword, bool? isVisibleInRanking, CancellationToken cancellationToken = default);
    Task RegisterTrainerAsync(string name, string email, string password, string confirmPassword, CancellationToken cancellationToken = default);
    Task<LoginResult> LoginAsync(string name, string password, CancellationToken cancellationToken = default);
    Task<LoginResult> LoginTrainerAsync(string name, string password, CancellationToken cancellationToken = default);
    Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserInfoResult> CheckTokenAsync(UserEntity currentUser, CancellationToken cancellationToken = default);
    Task<List<RankingEntry>> GetUsersRankingAsync(CancellationToken cancellationToken = default);
    Task<int> GetUserEloAsync(Guid userId, CancellationToken cancellationToken = default);
    Task LogoutAsync(UserEntity currentUser, CancellationToken cancellationToken = default);
    Task DeleteAccountAsync(UserEntity currentUser, CancellationToken cancellationToken = default);
    Task ChangeVisibilityInRankingAsync(UserEntity currentUser, bool isVisibleInRanking, CancellationToken cancellationToken = default);
    Task UpdateUserRolesAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
}
