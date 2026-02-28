namespace LgymApi.BackgroundWorker.Common.Outbox;

public interface IOutboxDeliveryHandler
{
    string EventType { get; }
    string HandlerName { get; }
    Task HandleAsync(Guid eventId, Guid correlationId, string payloadJson, CancellationToken cancellationToken = default);
}
