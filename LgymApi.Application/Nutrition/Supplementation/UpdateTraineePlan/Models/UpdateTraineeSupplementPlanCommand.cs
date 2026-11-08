using LgymApi.Application.Nutrition.Supplementation.Models;
using LgymApi.Authorization.Contracts;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Nutrition.Supplementation.UpdateTraineePlan;

public sealed record UpdateTraineeSupplementPlanCommand(
    AuthorizationActor Actor,
    Id<UserEntity> TrainerId,
    Id<UserEntity> TraineeId,
    Id<SupplementPlan> PlanId,
    SupplementPlanUpsertData Data);
