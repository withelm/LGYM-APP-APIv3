namespace LgymApi.BackgroundWorker.Common.Outbox;

public interface IOutboxDeliveryHandler
{
    OutboxEventDefinition EventDefinition { get; }
    string HandlerName { get; }
    Task HandleAsync(Guid eventId, Guid correlationId, string payloadJson, CancellationToken cancellationToken = default);
}
