using LgymApi.Api.Hubs;
using LgymApi.Api.Features.InAppNotification.Contracts;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using Microsoft.AspNetCore.SignalR;

namespace LgymApi.Api.Features.InAppNotification;

internal sealed class SignalRNotificationPushPublisher : IInAppNotificationPushPublisher
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<SignalRNotificationPushPublisher> _logger;

    public SignalRNotificationPushPublisher(
        IHubContext<NotificationHub> hubContext,
        ILogger<SignalRNotificationPushPublisher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task PushAsync(InAppNotificationResult notification, CancellationToken ct = default)
    {
        var payload = new InAppNotificationResultDto(
            notification.Id.ToString(),
            notification.Message,
            notification.RedirectUrl,
            notification.IsRead,
            notification.Type.Value,
            notification.IsSystemNotification,
            notification.SenderUserId?.ToString(),
            notification.CreatedAt);

        try
        {
            await _hubContext.Clients
                .Group($"user-{notification.RecipientId}")
                .SendAsync("ReceiveNotification", payload, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push in-app notification to user {RecipientId}", notification.RecipientId);
        }
    }
}
