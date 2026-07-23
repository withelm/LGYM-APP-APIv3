using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.Coaching.Invitations.Accept;

public interface IAcceptInvitationUseCase
{
    Task<Result<Unit, AppError>> ExecuteAsync(AcceptInvitationCommand command, CancellationToken cancellationToken = default);
}
