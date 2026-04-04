using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.Plan;

public interface IPlanService
{
    Task<Result<Unit, AppError>> CreatePlanAsync(UserEntity currentUser, Id<UserEntity> routeUserId, string name, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> UpdatePlanAsync(UserEntity currentUser, Id<UserEntity> routeUserId, Id<PlanEntity> planId, string name, CancellationToken cancellationToken = default);
    Task<Result<PlanEntity, AppError>> GetPlanConfigAsync(UserEntity currentUser, Id<UserEntity> routeUserId, CancellationToken cancellationToken = default);
    Task<Result<bool, AppError>> CheckIsUserHavePlanAsync(UserEntity currentUser, Id<UserEntity> routeUserId, CancellationToken cancellationToken = default);
    Task<Result<List<PlanEntity>, AppError>> GetPlansListAsync(UserEntity currentUser, Id<UserEntity> routeUserId, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> SetNewActivePlanAsync(UserEntity currentUser, Id<UserEntity> routeUserId, Id<PlanEntity> planId, CancellationToken cancellationToken = default);
    Task<Result<Unit, AppError>> DeletePlanAsync(UserEntity currentUser, Id<PlanEntity> planId, CancellationToken cancellationToken = default);
    Task<Result<PlanEntity, AppError>> CopyPlanAsync(UserEntity currentUser, string shareCode, CancellationToken cancellationToken = default);
    Task<Result<string, AppError>> GenerateShareCodeAsync(UserEntity currentUser, Id<PlanEntity> planId, CancellationToken cancellationToken = default);
}
