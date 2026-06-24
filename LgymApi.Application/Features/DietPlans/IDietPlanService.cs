using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.DietPlans.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.DietPlans;

public interface IDietPlanService
{
    Task<Result<List<DietPlanResult>, AppError>> GetTraineePlansAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, CancellationToken cancellationToken = default);
    Task<Result<DietPlanResult, AppError>> GetTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<DietPlan> dietPlanId, CancellationToken cancellationToken = default);
    Task<Result<DietPlanResult, AppError>> CreateTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, UpsertDietPlanCommand command, CancellationToken cancellationToken = default);
    Task<Result<DietPlanResult, AppError>> UpdateTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<DietPlan> dietPlanId, UpsertDietPlanCommand command, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> ActivateTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<DietPlan> dietPlanId, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> DeleteTraineePlanAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<DietPlan> dietPlanId, CancellationToken cancellationToken = default);
    Task<Result<List<DietPlanHistoryResult>, AppError>> GetTraineePlanHistoryAsync(UserEntity currentTrainer, Id<UserEntity> traineeId, Id<DietPlan> dietPlanId, CancellationToken cancellationToken = default);
    Task<Result<List<DietPlanResult>, AppError>> GetCurrentPlansAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default);
    Task<Result<DietPlanResult, AppError>> GetCurrentPlanAsync(UserEntity currentTrainee, CancellationToken cancellationToken = default);
}
