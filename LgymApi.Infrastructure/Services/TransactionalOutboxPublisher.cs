using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;

namespace LgymApi.Infrastructure.Services;

public sealed class TransactionalOutboxPublisher : ITransactionalOutboxPublisher
{
    private readonly IOutboxRepository _outboxRepository;

    public TransactionalOutboxPublisher(IOutboxRepository outboxRepository)
    {
        _outboxRepository = outboxRepository;
    }

    public async Task<Guid> PublishAsync(OutboxEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = envelope.EventType,
            PayloadJson = envelope.PayloadJson,
            CorrelationId = envelope.CorrelationId,
            NextAttemptAt = envelope.NextAttemptAt
        };

        await _outboxRepository.AddMessageAsync(message, cancellationToken);
        return message.Id;
    }
}
