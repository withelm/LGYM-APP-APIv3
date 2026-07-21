using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.WorkoutProgress.Dashboard.Models;
using LgymApi.Application.WorkoutProgress.ProgressData.Models;
using LgymApi.Application.Pagination;
using LgymApi.Domain.ValueObjects;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using TrainerInvitationEntity = LgymApi.Domain.Entities.TrainerInvitation;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.TrainerRelationships;

public interface ITrainerRelationshipService
{
    Task<Result<TrainerInvitationResult, AppError>> CreateInvitationAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
    Task<Result<TrainerInvitationResult, AppError>> CreateInvitationByEmailAsync(UserEntity currentTrainer, string inviteeEmail, string preferredLanguage, string preferredTimeZone, CancellationToken cancellationToken = default);
    Task<Result<List<TrainerInvitationResult>, AppError>> GetTrainerInvitationsAsync(UserEntity currentTrainer, CancellationToken cancellationToken = default);
    Task<Result<Pagination<TrainerInvitationResult>, AppError>> GetInvitationsPaginatedAsync(UserEntity currentTrainer, FilterInput filterInput, CancellationToken cancellationToken = default);
    Task<Result<TrainerDashboardTraineeListResult, AppError>> GetDashboardTraineesAsync(UserEntity currentTrainer, TrainerDashboardTraineeQuery query, CancellationToken cancellationToken = default);
    Task<Result<List<DateTime>, AppError>> GetTraineeTrainingDatesAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
    Task<Result<List<WorkoutProgressDashboardTrainingReadModel>, AppError>> GetTraineeTrainingByDateAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, DateTime createdAt, CancellationToken cancellationToken = default);
    Task<Result<List<ExerciseScoreChartPoint>, AppError>> GetTraineeExerciseScoresChartDataAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, string exerciseId, CancellationToken cancellationToken = default);
    Task<Result<List<EloChartPoint>, AppError>> GetTraineeEloChartAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
    Task<Result<List<MainRecordReadModel>, AppError>> GetTraineeMainRecordsHistoryAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
    Task<Result<List<TrainerManagedPlanResult>, AppError>> GetTraineePlansAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
    Task<Result<TrainerManagedPlanResult, AppError>> CreateTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, string name, CancellationToken cancellationToken = default);
    Task<Result<TrainerManagedPlanResult, AppError>> UpdateTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<PlanEntity> planId, string name, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> DeleteTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<PlanEntity> planId, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> AssignTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<PlanEntity> planId, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> UnassignTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
    Task<Result<TraineeTrainerProfileResult, AppError>> GetCurrentTrainerAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default);
    Task<Result<TrainerManagedPlanResult, AppError>> GetActiveAssignedPlanAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> AcceptInvitationAsync(UserEntity currentTrainee, Id<TrainerInvitationEntity> invitationId, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> RejectInvitationAsync(UserEntity currentTrainee, Id<TrainerInvitationEntity> invitationId, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> RevokeInvitationAsync(UserEntity currentTrainer, Id<TrainerInvitationEntity> invitationId, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> UnlinkTraineeAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> DetachFromTrainerAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default);
}
