using LgymApi.Authorization.Contracts;
using LgymApi.Identity.Contracts.Authorization;

namespace LgymApi.Application.Identity.Authorization;

internal sealed class GlobalPermissionAuthorizationPolicy : IAuthorizationPolicy<GlobalPermissionAuthorizationContext>
{
    public AuthorizationDecision Evaluate(AuthorizationActor actor, GlobalPermissionAuthorizationContext context)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);

        return context.PermissionClaims.Contains(context.Permission, StringComparer.Ordinal)
            ? AuthorizationDecision.Allow
            : AuthorizationDecision.Deny;
    }
}
