using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface ITrainerRelationshipRepository
{
    Task AddInvitationAsync(TrainerInvitation invitation, CancellationToken cancellationToken = default);
    Task<TrainerInvitation?> FindInvitationByIdAsync(Guid invitationId, CancellationToken cancellationToken = default);
    Task<TrainerInvitation?> FindPendingInvitationAsync(Guid trainerId, Guid traineeId, CancellationToken cancellationToken = default);
    Task<List<TrainerInvitation>> GetInvitationsByTrainerIdAsync(Guid trainerId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveLinkForTraineeAsync(Guid traineeId, CancellationToken cancellationToken = default);
    Task<TrainerTraineeLink?> FindActiveLinkByTrainerAndTraineeAsync(Guid trainerId, Guid traineeId, CancellationToken cancellationToken = default);
    Task<TrainerTraineeLink?> FindActiveLinkByTraineeIdAsync(Guid traineeId, CancellationToken cancellationToken = default);
    Task AddLinkAsync(TrainerTraineeLink link, CancellationToken cancellationToken = default);
    Task RemoveLinkAsync(TrainerTraineeLink link, CancellationToken cancellationToken = default);
}
