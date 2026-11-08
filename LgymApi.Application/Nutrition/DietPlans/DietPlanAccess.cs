using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Coaching.Errors;
using LgymApi.Authorization.Contracts;
using LgymApi.Platform.Contracts;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Nutrition.DietPlans;

internal enum DietPlanAction
{
    Read,
    ReadHistory,
    Create,
    Update,
    Activate,
    Delete
}

internal sealed record DietPlanAuthorizationContext(
    Id<ActorReference> TraineeId,
    bool ActorIsTrainer,
    bool HasActiveRelationship,
    DietPlanAction Action);

internal sealed class DietPlanAuthorizationPolicy : IAuthorizationPolicy<DietPlanAuthorizationContext>
{
    public AuthorizationDecision Evaluate(AuthorizationActor actor, DietPlanAuthorizationContext context)
    {
        if (actor.Id == context.TraineeId)
        {
            return context.Action is DietPlanAction.Read or DietPlanAction.ReadHistory or DietPlanAction.Update
                ? AuthorizationDecision.Allow
                : AuthorizationDecision.Deny;
        }

        return context.ActorIsTrainer && context.HasActiveRelationship
            ? AuthorizationDecision.Allow
            : AuthorizationDecision.Deny;
    }
}

internal interface IDietPlanAuthorizationCoordinator
{
    AuthorizationDecision Evaluate(AuthorizationActor actor, Id<UserEntity> traineeId, bool isTrainer, bool hasActiveRelationship, DietPlanAction action);
}

internal sealed class DietPlanAuthorizationCoordinator(
    IAuthorizationEvaluator<DietPlanAuthorizationContext> evaluator) : IDietPlanAuthorizationCoordinator
{
    public AuthorizationDecision Evaluate(AuthorizationActor actor, Id<UserEntity> traineeId, bool isTrainer, bool hasActiveRelationship, DietPlanAction action)
        => evaluator.Evaluate(actor, new DietPlanAuthorizationContext(traineeId.Rebind<ActorReference>(), isTrainer, hasActiveRelationship, action));
}

internal static class DietPlanAccess
{
    public static AppError? GetAccessError(
        IDietPlanAuthorizationCoordinator coordinator,
        AuthorizationActor actor,
        DietPlanAction action,
        Id<UserEntity> actorId,
        Id<UserEntity> traineeId,
        bool isTrainer,
        bool hasActiveRelationship)
    {
        var decision = coordinator.Evaluate(actor, traineeId, isTrainer, hasActiveRelationship, action);
        if (decision.IsAllowed)
        {
            return null;
        }

        if (actorId == traineeId || !isTrainer)
        {
            return new TrainerRelationshipForbiddenError(Messages.TrainerRoleRequired);
        }

        return new NotFoundError(Messages.DidntFind);
    }

    public static bool IsOwnedByTrainee(DietPlan plan, Id<UserEntity> traineeId)
        => plan.TraineeId == traineeId && !plan.IsDeleted;

}
