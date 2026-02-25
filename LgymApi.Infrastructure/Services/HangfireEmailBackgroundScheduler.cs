using Hangfire;
using LgymApi.Application.Notifications;
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
        _backgroundJobClient.Enqueue<EmailJob>(job => job.ExecuteAsync(notificationId));
    }
}
