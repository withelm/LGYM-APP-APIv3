using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface IOutboxRepository
{
    Task AddMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task AddDeliveryAsync(OutboxDelivery delivery, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxMessage>> GetDispatchableMessagesAsync(int batchSize, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<bool> TryMarkMessageProcessingAsync(Guid messageId, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<OutboxMessage?> FindMessageByIdAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task<OutboxDelivery?> FindDeliveryAsync(Guid eventId, string handlerName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxDelivery>> GetDispatchableDeliveriesAsync(int batchSize, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<bool> TryMarkDeliveryProcessingAsync(Guid deliveryId, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<OutboxDelivery?> FindDeliveryByIdWithEventAsync(Guid deliveryId, CancellationToken cancellationToken = default);
}
