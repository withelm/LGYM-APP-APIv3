using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Infrastructure.Services;

public sealed class NoOpEmailBackgroundScheduler : IEmailBackgroundScheduler
{
    public string? Enqueue(Id<NotificationMessage> notificationId)
    {
        return $"noop-email-{notificationId}";
    }
}
