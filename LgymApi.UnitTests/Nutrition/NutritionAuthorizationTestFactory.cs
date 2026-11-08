using LgymApi.Application.Nutrition.DietPlans;
using LgymApi.Application.Nutrition.Supplementation;
using LgymApi.Authorization;
using LgymApi.Authorization.Contracts;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Platform.Contracts;

namespace LgymApi.UnitTests.Nutrition;

internal static class NutritionAuthorizationTestFactory
{
    public static AuthorizationActor CreateActor(Id<User> userId) =>
        new(userId.Rebind<ActorReference>());

    public static IDietPlanAuthorizationCoordinator CreateDietCoordinator() =>
        new DietPlanAuthorizationCoordinator(
            new AuthorizationPolicyEvaluator<DietPlanAuthorizationContext>([new DietPlanAuthorizationPolicy()]));

    public static ISupplementationAuthorizationCoordinator CreateSupplementationCoordinator() =>
        new SupplementationAuthorizationCoordinator(
            new AuthorizationPolicyEvaluator<SupplementationAuthorizationContext>([new SupplementationAuthorizationPolicy()]));
}
