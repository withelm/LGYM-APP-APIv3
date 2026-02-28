using Hangfire;
using LgymApi.BackgroundWorker.Common.Outbox;

namespace LgymApi.Infrastructure.Jobs;

public sealed class OutboxDeliveryJob : IOutboxDeliveryJob
{
    private readonly IOutboxDeliveryProcessor _processor;

    public OutboxDeliveryJob(IOutboxDeliveryProcessor processor)
    {
        _processor = processor;
    }

    [AutomaticRetry(Attempts = 0)]
    public Task ExecuteAsync(Guid deliveryId)
    {
        return _processor.ProcessAsync(deliveryId);
    }
}
