using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;
using UserSessionEntity = LgymApi.Domain.Entities.UserSession;

namespace LgymApi.Application.Identity.Contracts.Sessions;

public interface IUserSessionTerminationService
{
    Task<Result<Unit, AppError>> LogoutAsync(
        UserEntity? currentUser,
        Id<UserSessionEntity>? sessionId,
        CancellationToken cancellationToken = default);
}
