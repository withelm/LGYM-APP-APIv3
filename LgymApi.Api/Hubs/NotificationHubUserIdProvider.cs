using LgymApi.Domain.Security;
using Microsoft.AspNetCore.SignalR;

namespace LgymApi.Api.Hubs;

public sealed class NotificationHubUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
        => connection.User?.FindFirst(AuthConstants.ClaimNames.UserId)?.Value;
}
