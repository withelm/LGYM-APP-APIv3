using LgymApi.Authorization.Contracts;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Platform.Contracts;

namespace LgymApi.Application.WorkoutProgress.Authorization;

public interface IWorkoutProgressOwnershipAuthorizationService
{
    bool IsOwner(Id<AccountReference> actorId, Id<AccountReference> ownerId);

    bool IsOwner(AuthenticatedAccountContext? actor, Id<AccountReference> ownerId);
}

internal sealed class WorkoutProgressOwnershipAuthorizationService(
    IAuthorizationEvaluator<WorkoutProgressOwnershipAuthorizationContext> evaluator)
    : IWorkoutProgressOwnershipAuthorizationService
{
    public bool IsOwner(Id<AccountReference> actorId, Id<AccountReference> ownerId)
    {
        if (actorId.IsEmpty || ownerId.IsEmpty)
        {
            return false;
        }

        return evaluator.Evaluate(
            new AuthorizationActor(actorId.Rebind<ActorReference>()),
            new WorkoutProgressOwnershipAuthorizationContext(ownerId.Rebind<ActorReference>())).IsAllowed;
    }

    public bool IsOwner(AuthenticatedAccountContext? actor, Id<AccountReference> ownerId)
        => actor is not null && IsOwner(actor.Id, ownerId);
}

internal sealed record WorkoutProgressOwnershipAuthorizationContext(Id<ActorReference> OwnerId);

internal sealed class WorkoutProgressOwnershipAuthorizationPolicy
    : IAuthorizationPolicy<WorkoutProgressOwnershipAuthorizationContext>
{
    public AuthorizationDecision Evaluate(
        AuthorizationActor actor,
        WorkoutProgressOwnershipAuthorizationContext context)
        => actor.Id == context.OwnerId
            ? AuthorizationDecision.Allow
            : AuthorizationDecision.Deny;
}
