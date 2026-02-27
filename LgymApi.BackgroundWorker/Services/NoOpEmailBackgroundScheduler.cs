using LgymApi.BackgroundWorker.Common;

namespace LgymApi.Infrastructure.Services;

public sealed class NoOpEmailBackgroundScheduler : IEmailBackgroundScheduler
{
    public void Enqueue(Guid notificationId)
    {
    }
}
