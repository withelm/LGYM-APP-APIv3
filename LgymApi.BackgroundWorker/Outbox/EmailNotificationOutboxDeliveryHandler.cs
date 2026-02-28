using System.Text.Json;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Outbox;

namespace LgymApi.BackgroundWorker.Outbox;

public sealed class EmailNotificationOutboxDeliveryHandler : IOutboxDeliveryHandler
{
    public const string Name = "email.notification.enqueue";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IEmailBackgroundScheduler _emailBackgroundScheduler;

    public EmailNotificationOutboxDeliveryHandler(IEmailBackgroundScheduler emailBackgroundScheduler)
    {
        _emailBackgroundScheduler = emailBackgroundScheduler;
    }

    public string EventType => OutboxEventTypes.EmailNotificationScheduled;
    public string HandlerName => Name;

    public Task HandleAsync(Guid eventId, Guid correlationId, string payloadJson, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Deserialize<EmailNotificationScheduledEvent>(payloadJson, SerializerOptions);
        if (payload == null || payload.NotificationId == Guid.Empty)
        {
            throw new InvalidOperationException($"Invalid payload for outbox event {eventId}.");
        }

        _emailBackgroundScheduler.Enqueue(payload.NotificationId);
        return Task.CompletedTask;
    }
}
