using LgymApi.Domain.ValueObjects;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.TrainingPlanning.Plan.ActivePlanPointer;

public interface IActivePlanPointerStore
{
    Task<Id<PlanEntity>?> GetActivePlanIdAsync(Id<UserEntity> userId, CancellationToken cancellationToken = default);

    Task StageActivePlanIdAsync(
        Id<UserEntity> userId,
        Id<PlanEntity>? planId,
        CancellationToken cancellationToken = default);
}
