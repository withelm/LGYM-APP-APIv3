using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;

namespace LgymApi.Api.Middleware;

public sealed class UserContextMiddleware
{
    private readonly RequestDelegate _next;

    public UserContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IUserRepository userRepository, IUserSessionStore userSessionStore)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null)
        {
            await _next(context);
            return;
        }

        var sidClaim = context.User.FindFirst("sid")?.Value;
        if (string.IsNullOrWhiteSpace(sidClaim) || !Id<UserSession>.TryParse(sidClaim, out var sessionId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = Messages.InvalidToken });
            return;
        }

        if (!await userSessionStore.ValidateSessionAsync(sessionId, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = Messages.Unauthorized });
            return;
        }

        var userIdClaim = context.User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Id<User>.TryParse(userIdClaim, out var userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = Messages.InvalidToken });
            return;
        }

        var user = await userRepository.FindByIdIncludingDeletedAsync(userId);
        if (user == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = Messages.InvalidToken });
            return;
        }

        if (user.IsDeleted)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = Messages.Unauthorized });
            return;
        }

        if (user.IsBlocked)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = Messages.AccountBlocked });
            return;
        }

        context.Items["User"] = user;
        await _next(context);
    }
}
