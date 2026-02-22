using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Features.ExerciseScores.Models;
using LgymApi.Application.Features.EloRegistry.Models;
using LgymApi.Application.Features.Training.Models;
using MainRecordEntity = LgymApi.Domain.Entities.MainRecord;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.TrainerRelationships;

public interface ITrainerRelationshipService
{
    Task<TrainerInvitationResult> CreateInvitationAsync(UserEntity currentTrainer, Guid traineeId, CancellationToken cancellationToken = default);
    Task<List<TrainerInvitationResult>> GetTrainerInvitationsAsync(UserEntity currentTrainer, CancellationToken cancellationToken = default);
    Task<TrainerDashboardTraineeListResult> GetDashboardTraineesAsync(UserEntity currentTrainer, TrainerDashboardTraineeQuery query, CancellationToken cancellationToken = default);
    Task<List<DateTime>> GetTraineeTrainingDatesAsync(UserEntity currentTrainer, Guid traineeId, CancellationToken cancellationToken = default);
    Task<List<TrainingByDateDetails>> GetTraineeTrainingByDateAsync(UserEntity currentTrainer, Guid traineeId, DateTime createdAt, CancellationToken cancellationToken = default);
    Task<List<ExerciseScoresChartData>> GetTraineeExerciseScoresChartDataAsync(UserEntity currentTrainer, Guid traineeId, Guid exerciseId, CancellationToken cancellationToken = default);
    Task<List<EloRegistryChartEntry>> GetTraineeEloChartAsync(UserEntity currentTrainer, Guid traineeId, CancellationToken cancellationToken = default);
    Task<List<MainRecordEntity>> GetTraineeMainRecordsHistoryAsync(UserEntity currentTrainer, Guid traineeId, CancellationToken cancellationToken = default);
    Task<List<TrainerManagedPlanResult>> GetTraineePlansAsync(UserEntity currentTrainer, Guid traineeId, CancellationToken cancellationToken = default);
    Task<TrainerManagedPlanResult> CreateTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, string name, CancellationToken cancellationToken = default);
    Task<TrainerManagedPlanResult> UpdateTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, Guid planId, string name, CancellationToken cancellationToken = default);
    Task DeleteTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, Guid planId, CancellationToken cancellationToken = default);
    Task AssignTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, Guid planId, CancellationToken cancellationToken = default);
    Task UnassignTraineePlanAsync(UserEntity currentTrainer, Guid traineeId, CancellationToken cancellationToken = default);
    Task<TrainerManagedPlanResult> GetActiveAssignedPlanAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default);
    Task AcceptInvitationAsync(UserEntity currentTrainee, Guid invitationId, CancellationToken cancellationToken = default);
    Task RejectInvitationAsync(UserEntity currentTrainee, Guid invitationId, CancellationToken cancellationToken = default);
    Task UnlinkTraineeAsync(UserEntity currentTrainer, Guid traineeId, CancellationToken cancellationToken = default);
    Task DetachFromTrainerAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default);
}
