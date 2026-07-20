using LgymApi.Domain.ValueObjects;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.TrainingPlanning.Plan.DeletePlan;

public sealed record DeletePlanCommand(Id<UserEntity> CurrentUserId, Id<PlanEntity> PlanId);
