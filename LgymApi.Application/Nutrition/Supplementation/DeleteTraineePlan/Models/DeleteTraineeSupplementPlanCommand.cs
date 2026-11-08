using LgymApi.Authorization.Contracts;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Nutrition.Supplementation.DeleteTraineePlan.Models;

public sealed record DeleteTraineeSupplementPlanCommand(
    AuthorizationActor Actor,
    Id<UserEntity> TrainerId,
    Id<UserEntity> TraineeId,
    Id<SupplementPlan> SupplementPlanId);
