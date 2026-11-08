using LgymApi.Application.Nutrition.Supplementation.Models;
using LgymApi.Authorization.Contracts;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Nutrition.Supplementation.CreateTraineePlan;

public sealed record CreateTraineeSupplementPlanCommand(
    AuthorizationActor Actor,
    Id<UserEntity> TrainerId,
    Id<UserEntity> TraineeId,
    SupplementPlanUpsertData Data);
