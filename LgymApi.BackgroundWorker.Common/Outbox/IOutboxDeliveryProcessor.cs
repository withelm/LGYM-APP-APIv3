namespace LgymApi.BackgroundWorker.Common.Outbox;

public interface IOutboxDeliveryProcessor
{
    Task ProcessAsync(Guid deliveryId, CancellationToken cancellationToken = default);
}
