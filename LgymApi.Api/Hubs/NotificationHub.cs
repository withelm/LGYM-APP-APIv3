using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace LgymApi.Api.Hubs;

[Authorize]
public sealed class NotificationHub : Hub
{
    private readonly IUserSessionStore _userSessionStore;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(IUserSessionStore userSessionStore, ILogger<NotificationHub> logger)
    {
        _userSessionStore = userSessionStore;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var sidClaim = Context.User?.FindFirst("sid")?.Value;
        if (sidClaim == null || !Id<UserSession>.TryParse(sidClaim, out var sessionId))
        {
            Context.Abort();
            return;
        }

        if (!await _userSessionStore.ValidateSessionAsync(sessionId, Context.ConnectionAborted))
        {
            Context.Abort();
            return;
        }

        var userId = Context.User?.FindFirst("userId")?.Value;
        if (userId == null || !Id<User>.TryParse(userId, out var userIdParsed))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(
            exception,
            "Notification hub disconnected for connection {ConnectionId}",
            Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    // Push-only hub — no client-to-server methods
}
