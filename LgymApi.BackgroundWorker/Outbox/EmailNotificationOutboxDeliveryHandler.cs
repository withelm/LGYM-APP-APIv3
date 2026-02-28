using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Outbox;

namespace LgymApi.BackgroundWorker.Outbox;

public sealed class EmailNotificationOutboxDeliveryHandler : IOutboxDeliveryHandler
{
    public const string Name = "email.notification.enqueue";

    private readonly IEmailBackgroundScheduler _emailBackgroundScheduler;

    public EmailNotificationOutboxDeliveryHandler(IEmailBackgroundScheduler emailBackgroundScheduler)
    {
        _emailBackgroundScheduler = emailBackgroundScheduler;
    }

    public OutboxEventDefinition EventDefinition => OutboxEventTypes.EmailNotificationScheduled;
    public string HandlerName => Name;

    public Task HandleAsync(Guid eventId, Guid correlationId, string payloadJson, CancellationToken cancellationToken = default)
    {
        var payload = OutboxEventTypes.EmailNotificationScheduled.Deserialize(payloadJson);
        if (payload == null || payload.NotificationId == Guid.Empty)
        {
            throw new InvalidOperationException($"Invalid payload for outbox event {eventId}.");
        }

        _emailBackgroundScheduler.Enqueue(payload.NotificationId);
        return Task.CompletedTask;
    }
}
