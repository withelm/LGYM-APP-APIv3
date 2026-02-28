namespace LgymApi.BackgroundWorker.Common.Outbox;

public interface IOutboxDeliveryJob
{
    Task ExecuteAsync(Guid deliveryId);
}
