using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;

namespace LgymApi.Application.Coaching.Invitations.PublicStatus;

public interface IPublicInvitationStatusUseCase
{
    Task<Result<PublicInvitationStatusReadModel, AppError>> ExecuteAsync(PublicInvitationStatusQuery query, CancellationToken cancellationToken = default);
}
