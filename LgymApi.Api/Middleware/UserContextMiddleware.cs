using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
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

        var sidClaim = context.User.FindFirst(AuthConstants.ClaimNames.SessionId)?.Value;
        if (string.IsNullOrWhiteSpace(sidClaim) || !Id<UserSession>.TryParse(sidClaim, out var sessionId))
        {
            await ErrorResponseWriter.WriteAsync(context, StatusCodes.Status401Unauthorized, Messages.InvalidToken, context.RequestAborted);
            return;
        }

        if (!await userSessionStore.ValidateSessionAsync(sessionId, context.RequestAborted))
        {
            await ErrorResponseWriter.WriteAsync(context, StatusCodes.Status401Unauthorized, Messages.Unauthorized, context.RequestAborted);
            return;
        }

        var userIdClaim = context.User.FindFirst(AuthConstants.ClaimNames.UserId)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Id<User>.TryParse(userIdClaim, out var userId))
        {
            await ErrorResponseWriter.WriteAsync(context, StatusCodes.Status401Unauthorized, Messages.InvalidToken, context.RequestAborted);
            return;
        }

        var user = await userRepository.FindByIdIncludingDeletedAsync(userId);
        if (user == null)
        {
            await ErrorResponseWriter.WriteAsync(context, StatusCodes.Status401Unauthorized, Messages.InvalidToken, context.RequestAborted);
            return;
        }

        if (user.IsDeleted)
        {
            await ErrorResponseWriter.WriteAsync(context, StatusCodes.Status401Unauthorized, Messages.Unauthorized, context.RequestAborted);
            return;
        }

        if (user.IsBlocked)
        {
            await ErrorResponseWriter.WriteAsync(context, StatusCodes.Status403Forbidden, Messages.AccountBlocked, context.RequestAborted);
            return;
        }

        context.Items["User"] = user;
        await _next(context);
    }
}
