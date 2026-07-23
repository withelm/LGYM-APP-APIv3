using LgymApi.Domain.ValueObjects;
using PlanEntity = LgymApi.Domain.Entities.Plan;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.TrainingPlanning.Contracts.ManagedPlans;

public sealed record DeleteManagedPlanCommand(
    Id<UserEntity> TrainerId,
    Id<UserEntity> TraineeId,
    Id<PlanEntity> PlanId);
