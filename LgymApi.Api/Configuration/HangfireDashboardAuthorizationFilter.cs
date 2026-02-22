using Hangfire.Dashboard;
using LgymApi.Domain.Security;
using System.Security.Claims;

namespace LgymApi.Api.Configuration;

public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var user = httpContext.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (user.IsInRole(AuthConstants.Roles.Admin))
        {
            return true;
        }

        return user.HasClaim(AuthConstants.PermissionClaimType, AuthConstants.Permissions.AdminAccess)
            || user.HasClaim(ClaimTypes.Role, AuthConstants.Roles.Admin);
    }
}
