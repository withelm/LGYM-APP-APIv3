using LgymApi.BackgroundWorker.Common.Outbox;

namespace LgymApi.Infrastructure.Services;

public sealed class NoOpOutboxDeliveryBackgroundScheduler : IOutboxDeliveryBackgroundScheduler
{
    public void Enqueue(Guid deliveryId)
    {
    }
}
