using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Nutrition.Errors;
using LgymApi.Authorization.Contracts;
using LgymApi.Domain.Entities;
using LgymApi.Platform.Contracts;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Nutrition.Supplementation;

internal sealed record SupplementationAuthorizationContext(
    Id<ActorReference> TraineeId,
    bool ActorIsTrainer,
    bool HasActiveRelationship);

internal sealed class SupplementationAuthorizationPolicy : IAuthorizationPolicy<SupplementationAuthorizationContext>
{
    public AuthorizationDecision Evaluate(AuthorizationActor actor, SupplementationAuthorizationContext context)
        => actor.Id != context.TraineeId && context.ActorIsTrainer && context.HasActiveRelationship
            ? AuthorizationDecision.Allow
            : AuthorizationDecision.Deny;
}

internal interface ISupplementationAuthorizationCoordinator
{
    AuthorizationDecision Evaluate(
        AuthorizationActor actor,
        Id<UserEntity> traineeId,
        bool isTrainer,
        bool hasActiveRelationship);
}

internal sealed class SupplementationAuthorizationCoordinator(
    IAuthorizationEvaluator<SupplementationAuthorizationContext> evaluator) : ISupplementationAuthorizationCoordinator
{
    public AuthorizationDecision Evaluate(
        AuthorizationActor actor,
        Id<UserEntity> traineeId,
        bool isTrainer,
        bool hasActiveRelationship)
        => evaluator.Evaluate(
            actor,
            new SupplementationAuthorizationContext(
                traineeId.Rebind<ActorReference>(),
                isTrainer,
                hasActiveRelationship));
}

internal static class SupplementationAccess
{
    public static AppError? GetTrainerAccessError(
        ISupplementationAuthorizationCoordinator coordinator,
        AuthorizationActor actor,
        bool isTrainer,
        bool hasActiveRelationship,
        Id<UserEntity> traineeId)
    {
        var decision = coordinator.Evaluate(actor, traineeId, isTrainer, hasActiveRelationship);
        if (!decision.IsAllowed && !isTrainer)
        {
            return new SupplementationForbiddenError(Messages.TrainerRoleRequired);
        }

        if (traineeId.IsEmpty)
        {
            return new InvalidSupplementationError(Messages.UserIdRequired);
        }

        return decision.IsAllowed
            ? null
            : new SupplementationNotFoundError(Messages.DidntFind);
    }

    public static bool IsOwnedBy(SupplementPlan plan, Id<UserEntity> trainerId, Id<UserEntity> traineeId)
        => plan.TrainerId == trainerId
           && plan.TraineeId == traineeId
           && !plan.IsDeleted;
}
