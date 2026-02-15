using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Plan;

public interface IPlanService
{
    Task CreatePlanAsync(UserEntity currentUser, Guid routeUserId, string name);
    Task UpdatePlanAsync(UserEntity currentUser, Guid routeUserId, string planId, string name);
    Task<PlanEntity> GetPlanConfigAsync(UserEntity currentUser, Guid routeUserId);
    Task<bool> CheckIsUserHavePlanAsync(UserEntity currentUser, Guid routeUserId);
    Task<List<PlanEntity>> GetPlansListAsync(UserEntity currentUser, Guid routeUserId);
    Task SetNewActivePlanAsync(UserEntity currentUser, Guid routeUserId, Guid planId);
    Task DeletePlanAsync(UserEntity currentUser, Guid planId);
    Task<PlanEntity> CopyPlanAsync(UserEntity currentUser, string shareCode);
    Task<string> GenerateShareCodeAsync(UserEntity currentUser, Guid planId);
}
