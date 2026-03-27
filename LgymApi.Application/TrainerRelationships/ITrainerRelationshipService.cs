using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Features.ExerciseScores.Models;
using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Application.Features.Training.Models;
using LgymApi.Domain.ValueObjects;
using ExerciseEntity = LgymApi.Domain.Entities.Exercise;
using MainRecordEntity = LgymApi.Domain.Entities.MainRecord;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using TrainerInvitationEntity = LgymApi.Domain.Entities.TrainerInvitation;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.TrainerRelationships;

public interface ITrainerRelationshipService
{
    Task<TrainerInvitationResult> CreateInvitationAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
    Task<List<TrainerInvitationResult>> GetTrainerInvitationsAsync(UserEntity currentTrainer, CancellationToken cancellationToken = default);
    Task<TrainerDashboardTraineeListResult> GetDashboardTraineesAsync(UserEntity currentTrainer, TrainerDashboardTraineeQuery query, CancellationToken cancellationToken = default);
    Task<List<DateTime>> GetTraineeTrainingDatesAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
    Task<List<TrainingByDateDetails>> GetTraineeTrainingByDateAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, DateTime createdAt, CancellationToken cancellationToken = default);
    Task<List<ExerciseScoresChartData>> GetTraineeExerciseScoresChartDataAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<ExerciseEntity> exerciseId, CancellationToken cancellationToken = default);
    Task<List<EloRegistryChartEntry>> GetTraineeEloChartAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
    Task<List<MainRecordEntity>> GetTraineeMainRecordsHistoryAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
    Task<List<TrainerManagedPlanResult>> GetTraineePlansAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
    Task<TrainerManagedPlanResult> CreateTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, string name, CancellationToken cancellationToken = default);
    Task<TrainerManagedPlanResult> UpdateTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<PlanEntity> planId, string name, CancellationToken cancellationToken = default);
    Task DeleteTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<PlanEntity> planId, CancellationToken cancellationToken = default);
    Task AssignTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<PlanEntity> planId, CancellationToken cancellationToken = default);
    Task UnassignTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
    Task<TrainerManagedPlanResult> GetActiveAssignedPlanAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default);
    Task AcceptInvitationAsync(UserEntity currentTrainee, Id<TrainerInvitationEntity> invitationId, CancellationToken cancellationToken = default);
    Task RejectInvitationAsync(UserEntity currentTrainee, Id<TrainerInvitationEntity> invitationId, CancellationToken cancellationToken = default);
    Task UnlinkTraineeAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
    Task DetachFromTrainerAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default);
}
