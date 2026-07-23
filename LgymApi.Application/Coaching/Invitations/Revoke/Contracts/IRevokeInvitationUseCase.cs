using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.Coaching.Invitations.Revoke;

public interface IRevokeInvitationUseCase
{
    Task<Result<Unit, AppError>> ExecuteAsync(RevokeInvitationCommand command, CancellationToken cancellationToken = default);
}
