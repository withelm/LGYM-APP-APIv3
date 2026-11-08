using LgymApi.Application.Nutrition.DietPlans.Models;
using LgymApi.Authorization.Contracts;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Nutrition.DietPlans.CreateTraineePlan;

public sealed record CreateTraineeDietPlanCommand(
    AuthorizationActor Actor,
    Id<UserEntity> TrainerId,
    Id<UserEntity> TraineeId,
    DietPlanUpsertData Data);
