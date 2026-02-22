using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Plan;

public interface IPlanService
{
    Task CreatePlanAsync(UserEntity currentUser, Guid routeUserId, string name, CancellationToken cancellationToken = default);
    Task UpdatePlanAsync(UserEntity currentUser, Guid routeUserId, string planId, string name, CancellationToken cancellationToken = default);
    Task<PlanEntity> GetPlanConfigAsync(UserEntity currentUser, Guid routeUserId, CancellationToken cancellationToken = default);
    Task<bool> CheckIsUserHavePlanAsync(UserEntity currentUser, Guid routeUserId, CancellationToken cancellationToken = default);
    Task<List<PlanEntity>> GetPlansListAsync(UserEntity currentUser, Guid routeUserId, CancellationToken cancellationToken = default);
    Task SetNewActivePlanAsync(UserEntity currentUser, Guid routeUserId, Guid planId, CancellationToken cancellationToken = default);
    Task DeletePlanAsync(UserEntity currentUser, Guid planId, CancellationToken cancellationToken = default);
    Task<PlanEntity> CopyPlanAsync(UserEntity currentUser, string shareCode, CancellationToken cancellationToken = default);
    Task<string> GenerateShareCodeAsync(UserEntity currentUser, Guid planId, CancellationToken cancellationToken = default);
}
