using LgymApi.Application.Nutrition.DietPlans.Models;
using LgymApi.Authorization.Contracts;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Nutrition.DietPlans.UpdateTraineePlan;

public sealed record UpdateTraineeDietPlanCommand(
    AuthorizationActor Actor,
    Id<User> TrainerId,
    Id<User> TraineeId,
    Id<DietPlan> DietPlanId,
    DietPlanUpsertData Data);
