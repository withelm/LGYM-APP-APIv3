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
    private readonly IUserSessionCache _userSessionCache;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(IUserSessionCache userSessionCache, ILogger<NotificationHub> logger)
    {
        _userSessionCache = userSessionCache;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("userId")?.Value;
        if (userId == null || !Id<User>.TryParse(userId, out var userIdParsed))
        {
            Context.Abort();
            return;
        }

        // Check session cache — reject if logged out
        if (!_userSessionCache.Contains(userIdParsed))
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
