using Hangfire;

namespace LgymApi.Infrastructure.Services;

public sealed class HangfireJobReconciler : IHangfireJobReconciler
{
    public Task<bool> ReconcileAsync(string schedulerJobId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schedulerJobId);

        return Task.FromResult(BackgroundJob.Delete(schedulerJobId));
    }
}
