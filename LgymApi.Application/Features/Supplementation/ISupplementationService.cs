using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Supplementation.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Supplementation;

public interface ISupplementationService
{
    Task<Result<List<SupplementPlanResult>, AppError>> GetTraineePlansAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
    Task<Result<SupplementPlanResult, AppError>> CreateTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, UpsertSupplementPlanCommand command, CancellationToken cancellationToken = default);
    Task<Result<SupplementPlanResult, AppError>> UpdateTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<SupplementPlan> planId, UpsertSupplementPlanCommand command, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> DeleteTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<SupplementPlan> planId, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> AssignTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<SupplementPlan> planId, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> UnassignTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
    Task<Result<List<SupplementScheduleEntryResult>, AppError>> GetActiveScheduleForDateAsync(UserEntity currentTrainee, DateOnly date, CancellationToken cancellationToken = default);
    Task<Result<SupplementScheduleEntryResult, AppError>> CheckOffIntakeAsync(UserEntity currentTrainee, CheckOffSupplementIntakeCommand command, CancellationToken cancellationToken = default);
    Task<Result<SupplementComplianceSummaryResult, AppError>> GetComplianceSummaryAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
}
