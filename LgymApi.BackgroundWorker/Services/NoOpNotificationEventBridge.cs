using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;

namespace LgymApi.BackgroundWorker.Services;

internal sealed class NoOpNotificationEventBridge : INotificationEventBridge
{
    public Task EnqueueAsync(EnqueueNotificationEventInput input, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
