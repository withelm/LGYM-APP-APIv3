namespace LgymApi.BackgroundWorker.Common.Outbox;

public interface ITransactionalOutboxPublisher
{
    Task<Guid> PublishAsync(OutboxEventEnvelope envelope, CancellationToken cancellationToken = default);

    Task<Guid> PublishAsync<TPayload>(
        OutboxEventDefinition<TPayload> eventDefinition,
        TPayload payload,
        Guid correlationId,
        DateTimeOffset? nextAttemptAt = null,
        CancellationToken cancellationToken = default);
}
