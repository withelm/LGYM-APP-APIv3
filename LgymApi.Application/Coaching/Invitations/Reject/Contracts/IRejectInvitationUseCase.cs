using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.Coaching.Invitations.Reject;

public interface IRejectInvitationUseCase
{
    Task<Result<Unit, AppError>> ExecuteAsync(RejectInvitationCommand command, CancellationToken cancellationToken = default);
}
