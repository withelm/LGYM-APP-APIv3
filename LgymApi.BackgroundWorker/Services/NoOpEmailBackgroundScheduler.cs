using System.Threading;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Infrastructure.Services;

public sealed class NoOpEmailBackgroundScheduler : IEmailBackgroundScheduler
{
    private long _sequence;

    public string? Enqueue(Id<NotificationMessage> notificationId)
    {
        var suffix = Interlocked.Increment(ref _sequence);
        return $"noop-email-{notificationId}-{suffix}";
    }
}
