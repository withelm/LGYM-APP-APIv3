using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Coaching.Invitations.Models;

namespace LgymApi.Application.Coaching.Invitations.List;

public interface IListInvitationsUseCase
{
    Task<Result<IReadOnlyList<InvitationReadModel>, AppError>> ExecuteAsync(ListInvitationsQuery query, CancellationToken cancellationToken = default);
}
