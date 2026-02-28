namespace LgymApi.BackgroundWorker.Common.Outbox;

public interface IOutboxDeliveryBackgroundScheduler
{
    void Enqueue(Guid deliveryId);
}
