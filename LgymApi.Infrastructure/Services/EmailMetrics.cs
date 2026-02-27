using System.Diagnostics.Metrics;
using LgymApi.BackgroundWorker.Common.Notifications;

namespace LgymApi.Infrastructure.Services;

public sealed class EmailMetrics : IEmailMetrics
{
    private static readonly Meter Meter = new("LgymApi.Email", "1.0.0");
    private static readonly Counter<long> EnqueuedCounter = Meter.CreateCounter<long>("email_enqueued_total");
    private static readonly Counter<long> SentCounter = Meter.CreateCounter<long>("email_sent_total");
    private static readonly Counter<long> FailedCounter = Meter.CreateCounter<long>("email_failed_total");
    private static readonly Counter<long> RetriedCounter = Meter.CreateCounter<long>("email_retried_total");

    public void RecordEnqueued(string notificationType)
    {
        EnqueuedCounter.Add(1, new KeyValuePair<string, object?>("notification_type", notificationType));
    }

    public void RecordSent(string notificationType)
    {
        SentCounter.Add(1, new KeyValuePair<string, object?>("notification_type", notificationType));
    }

    public void RecordFailed(string notificationType)
    {
        FailedCounter.Add(1, new KeyValuePair<string, object?>("notification_type", notificationType));
    }

    public void RecordRetried(string notificationType)
    {
        RetriedCounter.Add(1, new KeyValuePair<string, object?>("notification_type", notificationType));
    }
}
