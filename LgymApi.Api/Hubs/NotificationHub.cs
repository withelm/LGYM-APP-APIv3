using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LgymApi.Api.Hubs;

[Authorize]
public sealed class NotificationHub : Hub
{
    private readonly IUserSessionCache _userSessionCache;

    public NotificationHub(IUserSessionCache userSessionCache)
    {
        _userSessionCache = userSessionCache;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("userId")?.Value;
        if (userId == null || !Guid.TryParse(userId, out var userGuid))
        {
            Context.Abort();
            return;
        }

        // Check session cache — reject if logged out
        if (!_userSessionCache.Contains(new Id<User>(userGuid)))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        await base.OnConnectedAsync();
    }

    // Push-only hub — no client-to-server methods
}
