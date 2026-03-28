using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.Plan;

public interface IPlanService
{
    Task CreatePlanAsync(UserEntity currentUser, Id<UserEntity> routeUserId, string name, CancellationToken cancellationToken = default);
    Task UpdatePlanAsync(UserEntity currentUser, Id<UserEntity> routeUserId, Id<PlanEntity> planId, string name, CancellationToken cancellationToken = default);
    Task<PlanEntity> GetPlanConfigAsync(UserEntity currentUser, Id<UserEntity> routeUserId, CancellationToken cancellationToken = default);
    Task<bool> CheckIsUserHavePlanAsync(UserEntity currentUser, Id<UserEntity> routeUserId, CancellationToken cancellationToken = default);
    Task<List<PlanEntity>> GetPlansListAsync(UserEntity currentUser, Id<UserEntity> routeUserId, CancellationToken cancellationToken = default);
    Task SetNewActivePlanAsync(UserEntity currentUser, Id<UserEntity> routeUserId, Id<PlanEntity> planId, CancellationToken cancellationToken = default);
    Task DeletePlanAsync(UserEntity currentUser, Id<PlanEntity> planId, CancellationToken cancellationToken = default);
    Task<PlanEntity> CopyPlanAsync(UserEntity currentUser, string shareCode, CancellationToken cancellationToken = default);
    Task<string> GenerateShareCodeAsync(UserEntity currentUser, Id<PlanEntity> planId, CancellationToken cancellationToken = default);
}
