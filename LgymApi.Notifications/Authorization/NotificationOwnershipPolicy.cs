using LgymApi.Authorization.Contracts;
using LgymApi.Domain.ValueObjects;
using LgymApi.Platform.Contracts;

namespace LgymApi.Application.Notifications.Authorization;

internal sealed record NotificationOwnershipContext(Id<ActorReference> RecipientId);

internal sealed class NotificationOwnershipPolicy : IAuthorizationPolicy<NotificationOwnershipContext>
{
    public AuthorizationDecision Evaluate(AuthorizationActor actor, NotificationOwnershipContext context)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);

        return actor.Id == context.RecipientId
            ? AuthorizationDecision.Allow
            : AuthorizationDecision.Deny;
    }
}
