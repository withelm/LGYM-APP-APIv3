using LgymApi.Application.Features.TrainerRelationships.Models;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.TrainerRelationships;

public interface ITrainerRelationshipService
{
    Task<TrainerInvitationResult> CreateInvitationAsync(UserEntity currentTrainer, Guid traineeId);
    Task<List<TrainerInvitationResult>> GetTrainerInvitationsAsync(UserEntity currentTrainer);
    Task<TrainerDashboardTraineeListResult> GetDashboardTraineesAsync(UserEntity currentTrainer, TrainerDashboardTraineeQuery query);
    Task AcceptInvitationAsync(UserEntity currentTrainee, Guid invitationId);
    Task RejectInvitationAsync(UserEntity currentTrainee, Guid invitationId);
    Task UnlinkTraineeAsync(UserEntity currentTrainer, Guid traineeId);
    Task DetachFromTrainerAsync(UserEntity currentTrainee);
}
