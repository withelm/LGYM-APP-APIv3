using LgymApi.Application.Exceptions;
using LgymApi.Resources;

namespace LgymApi.Api.Middleware;

public static class RouteUserAccessGuard
{
    public static Guid ParseRouteUserIdForCurrentUser(this HttpContext context, string routeUserId)
    {
        var currentUserId = context.GetCurrentUser()?.Id ?? Guid.Empty;
        if (currentUserId == Guid.Empty)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        if (!Guid.TryParse(routeUserId, out var parsedRouteUserId))
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        if (parsedRouteUserId != currentUserId)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        return parsedRouteUserId;
    }

    public static Guid ParseRouteUserIdForCurrentAdmin(this HttpContext context, string routeUserId)
    {
        var currentUser = context.GetCurrentUser();
        if (currentUser == null || currentUser.Id == Guid.Empty || currentUser.Admin != true)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        if (!Guid.TryParse(routeUserId, out var parsedRouteUserId))
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        if (parsedRouteUserId != currentUser.Id)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        return parsedRouteUserId;
    }
}
