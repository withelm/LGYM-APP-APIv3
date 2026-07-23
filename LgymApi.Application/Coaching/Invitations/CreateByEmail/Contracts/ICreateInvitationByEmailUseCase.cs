using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Coaching.Invitations.Models;

namespace LgymApi.Application.Coaching.Invitations.CreateByEmail;

public interface ICreateInvitationByEmailUseCase
{
    Task<Result<InvitationReadModel, AppError>> ExecuteAsync(CreateInvitationByEmailCommand command, CancellationToken cancellationToken = default);
}
