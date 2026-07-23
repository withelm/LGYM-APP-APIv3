using LgymApi.Domain.ValueObjects;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Coaching.ManagedPlans.Assign;

public sealed record AssignTraineeManagedPlanCommand(
    Id<UserEntity> TrainerId,
    Id<UserEntity> TraineeId,
    Id<PlanEntity> PlanId);
