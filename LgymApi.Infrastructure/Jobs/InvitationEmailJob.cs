using Hangfire;
using LgymApi.Application.Notifications;

namespace LgymApi.Infrastructure.Jobs;

public sealed class InvitationEmailJob
{
    private readonly IInvitationEmailJobHandler _handler;

    public InvitationEmailJob(IInvitationEmailJobHandler handler)
    {
        _handler = handler;
    }

    [AutomaticRetry(Attempts = 3)]
    public Task ExecuteAsync(Guid notificationId)
    {
        return _handler.ProcessAsync(notificationId);
    }
}
