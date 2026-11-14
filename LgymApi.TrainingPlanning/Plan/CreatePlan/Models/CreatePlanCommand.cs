using LgymApi.Domain.ValueObjects;
using LgymApi.Domain.Enums;
using LgymApi.Identity.Contracts;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.TrainingPlanning.Plan.CreatePlan;

public sealed record CreatePlanCommand(
    Id<AccountReference> CurrentUserId,
    Id<AccountReference> RouteUserId,
    string Name,
    PlanPurpose Purpose = PlanPurpose.Unclassified)
{
    internal CreatePlanCommand(Id<UserEntity> currentUserId, Id<UserEntity> routeUserId, string name)
        : this(currentUserId.Rebind<AccountReference>(), routeUserId.Rebind<AccountReference>(), name)
    {
    }
}
