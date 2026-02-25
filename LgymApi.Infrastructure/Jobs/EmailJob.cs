using Hangfire;
using LgymApi.Application.Notifications;

namespace LgymApi.Infrastructure.Jobs;

public sealed class EmailJob
{
    private readonly IEmailJobHandler _handler;

    public EmailJob(IEmailJobHandler handler)
    {
        _handler = handler;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public Task ExecuteAsync(Guid notificationId)
    {
        return _handler.ProcessAsync(notificationId);
    }
}
