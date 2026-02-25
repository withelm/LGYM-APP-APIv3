using Hangfire;
using LgymApi.Application.Notifications;
using LgymApi.Infrastructure.Jobs;

namespace LgymApi.Infrastructure.Services;

public sealed class HangfireInvitationEmailBackgroundScheduler : IInvitationEmailBackgroundScheduler
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public HangfireInvitationEmailBackgroundScheduler(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public void Enqueue(Guid notificationId)
    {
        _backgroundJobClient.Enqueue<InvitationEmailJob>(job => job.ExecuteAsync(notificationId));
    }
}
