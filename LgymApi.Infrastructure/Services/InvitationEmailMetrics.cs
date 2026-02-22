using System.Diagnostics.Metrics;
using LgymApi.Application.Notifications;

namespace LgymApi.Infrastructure.Services;

public sealed class InvitationEmailMetrics : IInvitationEmailMetrics
{
    private static readonly Meter Meter = new("LgymApi.InvitationEmail", "1.0.0");
    private static readonly Counter<long> EnqueuedCounter = Meter.CreateCounter<long>("invitation_email_enqueued_total");
    private static readonly Counter<long> SentCounter = Meter.CreateCounter<long>("invitation_email_sent_total");
    private static readonly Counter<long> FailedCounter = Meter.CreateCounter<long>("invitation_email_failed_total");
    private static readonly Counter<long> RetriedCounter = Meter.CreateCounter<long>("invitation_email_retried_total");

    public void RecordEnqueued()
    {
        EnqueuedCounter.Add(1);
    }

    public void RecordSent()
    {
        SentCounter.Add(1);
    }

    public void RecordFailed()
    {
        FailedCounter.Add(1);
    }

    public void RecordRetried()
    {
        RetriedCounter.Add(1);
    }
}
