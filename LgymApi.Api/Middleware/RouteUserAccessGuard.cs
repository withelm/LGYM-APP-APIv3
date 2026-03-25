using LgymApi.Application.Exceptions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;

namespace LgymApi.Api.Middleware;

public static class RouteUserAccessGuard
{
    public static Guid ParseRouteUserIdForCurrentUser(this HttpContext context, string routeUserId)
    {
        var currentUser = context.GetCurrentUser();
        if (currentUser == null || currentUser.Id.IsEmpty)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        if (!Guid.TryParse(routeUserId, out var parsedRouteUserId))
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        if (parsedRouteUserId != (Guid)currentUser.Id)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        return parsedRouteUserId;
    }

    public static Guid ParseRouteUserIdForCurrentAdmin(this HttpContext context, string routeUserId)
    {
        var currentUser = context.GetCurrentUser();
        if (currentUser == null || currentUser.Id.IsEmpty)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        if (!Guid.TryParse(routeUserId, out var parsedRouteUserId))
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        if (parsedRouteUserId != (Guid)currentUser.Id)
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        return parsedRouteUserId;
    }
}
