using LgymApi.Authorization.Contracts;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Nutrition.Supplementation.UnassignTraineePlan;

public sealed record UnassignTraineeSupplementPlanCommand(
    AuthorizationActor Actor,
    Id<UserEntity> TrainerId,
    Id<UserEntity> TraineeId);
