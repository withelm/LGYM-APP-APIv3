using LgymApi.Domain.ValueObjects;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.TrainingPlanning.Plan.SetActivePlan;

public sealed record SetActivePlanCommand(
    Id<UserEntity> CurrentUserId,
    Id<UserEntity> RouteUserId,
    Id<PlanEntity> PlanId);
