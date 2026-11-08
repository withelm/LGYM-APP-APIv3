using LgymApi.Application.Nutrition.DietPlans.Models;
using LgymApi.Authorization.Contracts;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Nutrition.DietPlans.UpdateOwnDietPlan;

public sealed record UpdateOwnDietPlanCommand(
    AuthorizationActor Actor,
    Id<User> TraineeId,
    Id<DietPlan> DietPlanId,
    DietPlanUpsertData Data);
