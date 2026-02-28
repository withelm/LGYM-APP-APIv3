using LgymApi.Domain.Notifications;

namespace LgymApi.BackgroundWorker.Common.Outbox;

public sealed record OutboxEventEnvelope(
    OutboxEventType EventType,
    string PayloadJson,
    Guid CorrelationId,
    DateTimeOffset? NextAttemptAt = null)
{
    public string EventTypeValue => EventType.Value;

    public static OutboxEventEnvelope From<TPayload>(
        OutboxEventDefinition<TPayload> eventDefinition,
        TPayload payload,
        Guid correlationId,
        DateTimeOffset? nextAttemptAt = null)
    {
        return new OutboxEventEnvelope(
            eventDefinition.EventType,
            eventDefinition.Serialize(payload),
            correlationId,
            nextAttemptAt);
    }
}
