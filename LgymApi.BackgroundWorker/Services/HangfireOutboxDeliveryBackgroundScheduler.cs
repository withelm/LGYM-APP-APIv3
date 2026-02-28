using Hangfire;
using LgymApi.BackgroundWorker.Common.Outbox;

namespace LgymApi.Infrastructure.Services;

public sealed class HangfireOutboxDeliveryBackgroundScheduler : IOutboxDeliveryBackgroundScheduler
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public HangfireOutboxDeliveryBackgroundScheduler(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public void Enqueue(Guid deliveryId)
    {
        _backgroundJobClient.Enqueue<IOutboxDeliveryJob>(job => job.ExecuteAsync(deliveryId));
    }
}
