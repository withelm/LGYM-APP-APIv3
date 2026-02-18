using LgymApi.Application.Features.User.Models;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.User;

public interface IUserService
{
    Task RegisterAsync(string name, string email, string password, string confirmPassword, bool? isVisibleInRanking);
    Task RegisterTrainerAsync(string name, string email, string password, string confirmPassword);
    Task<LoginResult> LoginAsync(string name, string password);
    Task<LoginResult> LoginTrainerAsync(string name, string password);
    Task<bool> IsAdminAsync(Guid userId);
    Task<UserInfoResult> CheckTokenAsync(UserEntity currentUser);
    Task<List<RankingEntry>> GetUsersRankingAsync();
    Task<int> GetUserEloAsync(Guid userId);
    Task LogoutAsync(UserEntity currentUser);
    Task DeleteAccountAsync(UserEntity currentUser);
    Task ChangeVisibilityInRankingAsync(UserEntity currentUser, bool isVisibleInRanking);
    Task UpdateUserRolesAsync(Guid userId, IReadOnlyCollection<string> roles);
}
