using Hangfire;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Jobs;

namespace LgymApi.Infrastructure.Services;

public sealed class HangfireEmailBackgroundScheduler : IEmailBackgroundScheduler
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public HangfireEmailBackgroundScheduler(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public string? Enqueue(Id<NotificationMessage> notificationId)
    {
        return _backgroundJobClient.Enqueue<IEmailJob>(job => job.ExecuteAsync(notificationId));
    }
}
