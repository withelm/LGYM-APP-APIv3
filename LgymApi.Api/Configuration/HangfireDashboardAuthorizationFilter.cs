using Hangfire.Dashboard;
using LgymApi.Api.Middleware;
using LgymApi.Domain.Security;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Authorization;
using LgymApi.Resources;

namespace LgymApi.Api.Configuration;

internal sealed class HangfireDashboardAuthorizationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IAuthenticatedAccountAuthorizationService authorizationService)
    {
        var accountContext = context.GetAuthenticatedAccountContext();
        if (accountContext?.SessionId is null ||
            !authorizationService.HasPermission(accountContext, AuthConstants.Permissions.AdminAccess))
        {
            await ErrorResponseWriter.WriteAsync(
                context,
                StatusCodes.Status403Forbidden,
                Messages.Forbidden,
                context.RequestAborted);
            return;
        }

        context.Features.Set(new HangfireDashboardAuthorizationFeature());
        await next(context);
    }
}

public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.GetAuthenticatedAccountContext()?.SessionId is not null &&
               httpContext.Features.Get<HangfireDashboardAuthorizationFeature>() is not null;
    }
}

internal sealed class HangfireDashboardAuthorizationFeature;
