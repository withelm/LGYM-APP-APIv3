using LgymApi.Api.Middleware;
using LgymApi.Identity.Contracts.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace LgymApi.Api.Authorization;

public sealed record CurrentPermissionRequirement(string Permission) : IAuthorizationRequirement;

internal sealed class CurrentPermissionAuthorizationHandler(
    IAuthenticatedAccountAuthorizationService authenticatedAccountAuthorizationService)
    : AuthorizationHandler<CurrentPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CurrentPermissionRequirement requirement)
    {
        if (context.Resource is HttpContext httpContext
            && httpContext.Features.Get<IAuthenticatedAccountContextFeature>() is { } accountFeature
            && authenticatedAccountAuthorizationService.HasPermission(accountFeature.Context, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
