using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Identity.Contracts.Administration;

public interface IUserRoleAdministrationService
{
    Task<Result<Unit, AppError>> UpdateUserRolesAsync(
        Id<UserEntity> targetUserId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);
}
