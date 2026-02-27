using Hangfire;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.Infrastructure.Jobs;

namespace LgymApi.Infrastructure.Services;

public sealed class HangfireEmailBackgroundScheduler : IEmailBackgroundScheduler
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public HangfireEmailBackgroundScheduler(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public void Enqueue(Guid notificationId)
    {
        _backgroundJobClient.Enqueue<IEmailJob>(job => job.ExecuteAsync(notificationId));
    }
}
