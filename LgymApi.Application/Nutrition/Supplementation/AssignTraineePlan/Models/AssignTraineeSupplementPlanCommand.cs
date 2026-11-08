using LgymApi.Authorization.Contracts;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Nutrition.Supplementation.AssignTraineePlan;

public sealed record AssignTraineeSupplementPlanCommand(
    AuthorizationActor Actor,
    Id<UserEntity> TrainerId,
    Id<UserEntity> TraineeId,
    Id<SupplementPlan> SupplementPlanId);
