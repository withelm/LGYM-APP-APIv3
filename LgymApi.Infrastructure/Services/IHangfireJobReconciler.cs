namespace LgymApi.Infrastructure.Services;

public interface IHangfireJobReconciler
{
    Task<bool> ReconcileAsync(string schedulerJobId, CancellationToken cancellationToken = default);
}
