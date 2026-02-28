namespace LgymApi.Application.Notifications.Models;

public sealed record OutboxEventEnvelope(
    string EventType,
    string PayloadJson,
    Guid CorrelationId,
    DateTimeOffset? NextAttemptAt = null);
