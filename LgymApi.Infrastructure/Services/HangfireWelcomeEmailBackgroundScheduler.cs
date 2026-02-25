using Hangfire;
using LgymApi.Application.Notifications;
using LgymApi.Infrastructure.Jobs;

namespace LgymApi.Infrastructure.Services;

public sealed class HangfireWelcomeEmailBackgroundScheduler : IWelcomeEmailBackgroundScheduler
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public HangfireWelcomeEmailBackgroundScheduler(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public void Enqueue(Guid notificationId)
    {
        _backgroundJobClient.Enqueue<WelcomeEmailJob>(job => job.ExecuteAsync(notificationId));
    }
}
