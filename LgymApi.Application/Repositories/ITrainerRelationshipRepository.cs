using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Pagination;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Repositories;

public interface ITrainerRelationshipRepository
{
    Task AddInvitationAsync(TrainerInvitation invitation, CancellationToken cancellationToken = default);
    Task<TrainerInvitation?> FindInvitationByIdAsync(Id<TrainerInvitation> invitationId, CancellationToken cancellationToken = default);
    Task<TrainerInvitation?> FindPendingInvitationAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default);
    Task<TrainerInvitation?> FindPendingInvitationByEmailAsync(Id<User> trainerId, string inviteeEmail, CancellationToken cancellationToken = default);
    Task<bool> IsEmailAlreadyTraineeAsync(Id<User> trainerId, string inviteeEmail, CancellationToken cancellationToken = default);
    Task<TrainerInvitation?> FindInvitationByIdWithCodeAsync(Id<TrainerInvitation> invitationId, string code, CancellationToken cancellationToken = default);
    Task<List<TrainerInvitationResult>> GetPendingInvitationsForTraineeAsync(Id<User> traineeId, string traineeEmail, CancellationToken cancellationToken = default);
    Task<List<TrainerInvitation>> GetInvitationsByTrainerIdAsync(Id<User> trainerId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveLinkForTraineeAsync(Id<User> traineeId, CancellationToken cancellationToken = default);
    Task<TrainerTraineeLink?> FindActiveLinkByTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default);
    Task<TrainerTraineeLink?> FindActiveLinkByTraineeIdAsync(Id<User> traineeId, CancellationToken cancellationToken = default);
    Task<TrainerDashboardTraineeListResult> GetDashboardTraineesAsync(Id<User> trainerId, TrainerDashboardTraineeQuery query, CancellationToken cancellationToken = default);
    Task<Pagination<TrainerInvitationResult>> GetInvitationsPaginatedAsync(Id<User> trainerId, FilterInput filterInput, CancellationToken cancellationToken = default);
    Task AddLinkAsync(TrainerTraineeLink link, CancellationToken cancellationToken = default);
    Task RemoveLinkAsync(TrainerTraineeLink link, CancellationToken cancellationToken = default);
}
