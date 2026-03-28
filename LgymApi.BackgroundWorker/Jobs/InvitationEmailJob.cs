using Hangfire;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Infrastructure.Jobs;

public sealed class InvitationEmailJob : IInvitationEmailJob
{
    private readonly IEmailJobHandler _handler;

    public InvitationEmailJob(IEmailJobHandler handler)
    {
        _handler = handler;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public Task ExecuteAsync(Id<NotificationMessage> notificationId)
    {
        return _handler.ProcessAsync(notificationId);
    }
}
