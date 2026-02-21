using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Features.ExerciseScores.Models;
using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Application.Features.Training.Models;
using MainRecordEntity = LgymApi.Domain.Entities.MainRecord;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.TrainerRelationships;

public interface ITrainerRelationshipService
{
    Task<TrainerInvitationResult> CreateInvitationAsync(UserEntity currentTrainer, Guid traineeId);
    Task<List<TrainerInvitationResult>> GetTrainerInvitationsAsync(UserEntity currentTrainer);
    Task<TrainerDashboardTraineeListResult> GetDashboardTraineesAsync(UserEntity currentTrainer, TrainerDashboardTraineeQuery query);
    Task<List<DateTime>> GetTraineeTrainingDatesAsync(UserEntity currentTrainer, Guid traineeId);
    Task<List<TrainingByDateDetails>> GetTraineeTrainingByDateAsync(UserEntity currentTrainer, Guid traineeId, DateTime createdAt);
    Task<List<ExerciseScoresChartData>> GetTraineeExerciseScoresChartDataAsync(UserEntity currentTrainer, Guid traineeId, Guid exerciseId);
    Task<List<EloRegistryChartEntry>> GetTraineeEloChartAsync(UserEntity currentTrainer, Guid traineeId);
    Task<List<MainRecordEntity>> GetTraineeMainRecordsHistoryAsync(UserEntity currentTrainer, Guid traineeId);
    Task<List<TrainerManagedPlanResult>> GetTraineePlansAsync(UserEntity currentTrainer, Guid traineeId);
    Task<TrainerManagedPlanResult> CreateTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, string name);
    Task<TrainerManagedPlanResult> UpdateTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, Guid planId, string name);
    Task DeleteTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, Guid planId);
    Task AssignTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, Guid planId);
    Task UnassignTraineePlanAsync(UserEntity currentTrainer, Guid traineeId);
    Task<TrainerManagedPlanResult> GetActiveAssignedPlanAsync(UserEntity currentTrainee);
    Task AcceptInvitationAsync(UserEntity currentTrainee, Guid invitationId);
    Task RejectInvitationAsync(UserEntity currentTrainee, Guid invitationId);
    Task UnlinkTraineeAsync(UserEntity currentTrainer, Guid traineeId);
    Task DetachFromTrainerAsync(UserEntity currentTrainee);
}
