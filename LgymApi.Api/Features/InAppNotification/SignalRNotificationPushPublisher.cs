using LgymApi.Api.Hubs;
using LgymApi.Api.Features.InAppNotification.Contracts;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using Microsoft.AspNetCore.SignalR;

namespace LgymApi.Api.Features.InAppNotification;

internal sealed class SignalRNotificationPushPublisher : IInAppNotificationPushPublisher
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IAccountSessionConnectionRegistry _connectionRegistry;
    private readonly IAuthenticatedAccountContextResolver _authenticatedAccountContextResolver;
    private readonly INotificationReplayAccessService _notificationAccess;
    private readonly IMapper _mapper;
    private readonly ILogger<SignalRNotificationPushPublisher> _logger;

    public SignalRNotificationPushPublisher(
        IHubContext<NotificationHub> hubContext,
        IAccountSessionConnectionRegistry connectionRegistry,
        IAuthenticatedAccountContextResolver authenticatedAccountContextResolver,
        INotificationReplayAccessService notificationAccess,
        IMapper mapper,
        ILogger<SignalRNotificationPushPublisher> logger)
    {
        _hubContext = hubContext;
        _connectionRegistry = connectionRegistry;
        _authenticatedAccountContextResolver = authenticatedAccountContextResolver;
        _notificationAccess = notificationAccess;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task PushAsync(InAppNotificationResult notification, CancellationToken ct = default)
    {
        var payload = _mapper.Map<InAppNotificationResult, InAppNotificationResultDto>(notification);

        foreach (var connection in _connectionRegistry.GetConnections(notification.RecipientId.Rebind<AccountReference>()))
        {
            var resolution = await _authenticatedAccountContextResolver.ResolveAsync(
                connection.AccountId,
                connection.SessionId,
                ct);

            if (resolution.Status != AuthenticatedAccountResolutionStatus.Active
                || resolution.Context?.Id != connection.AccountId)
            {
                _connectionRegistry.Remove(connection.ConnectionId);
                continue;
            }

            if (!await _notificationAccess.CanReplayNotificationAsync(resolution.Context, notification.Id, ct))
            {
                continue;
            }

            try
            {
                await _hubContext.Clients.Client(connection.ConnectionId)
                    .SendAsync("ReceiveNotification", payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to push in-app notification to connection {ConnectionId} for user {RecipientId}",
                    connection.ConnectionId,
                    notification.RecipientId);
            }
        }
    }
}
