using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Coaching.Invitations.Models;

namespace LgymApi.Application.Coaching.Invitations.Create;

public interface ICreateInvitationUseCase
{
    Task<Result<InvitationReadModel, AppError>> ExecuteAsync(CreateInvitationCommand command, CancellationToken cancellationToken = default);
}
