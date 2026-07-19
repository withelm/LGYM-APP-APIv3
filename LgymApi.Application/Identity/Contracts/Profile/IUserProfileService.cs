using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.User.Models;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Identity.Contracts.Profile;

public interface IUserProfileService
{
    Task<Result<UserInfoResult, AppError>> CheckTokenAsync(UserEntity? currentUser, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> DeleteAccountAsync(UserEntity? currentUser, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> UpdateTimeZoneAsync(UserEntity? currentUser, string preferredTimeZone, CancellationToken cancellationToken = default);
}
