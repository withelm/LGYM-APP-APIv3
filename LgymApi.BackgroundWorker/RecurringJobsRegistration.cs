using Hangfire;
using LgymApi.BackgroundWorker.Common.Outbox;

namespace LgymApi.BackgroundWorker;

public static class RecurringJobsRegistration
{
    public static void RegisterOutboxDispatcher()
    {
        RecurringJob.AddOrUpdate<IOutboxDispatcherJob>(
            "outbox-dispatcher",
            job => job.ExecuteAsync(CancellationToken.None),
            Cron.Minutely());
    }
}
