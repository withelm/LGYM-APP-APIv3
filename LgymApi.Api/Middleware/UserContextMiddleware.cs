using LgymApi.Application.Repositories;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Middleware;

public sealed class UserContextMiddleware
{
    private readonly RequestDelegate _next;

    public UserContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IUserRepository userRepository)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.Contains("/login", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/register", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var userIdClaim = context.User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = Message.InvalidToken });
            return;
        }

        var user = await userRepository.FindByIdAsync(userId);
        if (user == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = Message.InvalidToken });
            return;
        }

        if (user.IsDeleted)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = Message.Unauthorized });
            return;
        }

        context.Items["User"] = user;
        await _next(context);
    }
}
