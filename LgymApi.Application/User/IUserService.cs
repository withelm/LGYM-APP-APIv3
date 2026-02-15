using LgymApi.Application.Features.User.Models;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.User;

public interface IUserService
{
    Task RegisterAsync(string name, string email, string password, string confirmPassword, bool? isVisibleInRanking);
    Task<LoginResult> LoginAsync(string name, string password);
    Task<bool> IsAdminAsync(Guid userId);
    Task<UserInfoResult> CheckTokenAsync(UserEntity currentUser);
    Task<List<RankingEntry>> GetUsersRankingAsync();
    Task<int> GetUserEloAsync(Guid userId);
    Task DeleteAccountAsync(UserEntity currentUser);
    Task ChangeVisibilityInRankingAsync(UserEntity currentUser, bool isVisibleInRanking);
}
