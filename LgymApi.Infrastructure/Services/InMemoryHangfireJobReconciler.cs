using System.Collections.Concurrent;

namespace LgymApi.Infrastructure.Services;

public sealed class InMemoryHangfireJobReconciler : IHangfireJobReconciler
{
    private readonly ConcurrentDictionary<string, byte> _deletedJobIds = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> DeletedJobIds => _deletedJobIds.Keys.ToArray();

    public Task<bool> ReconcileAsync(string schedulerJobId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schedulerJobId);

        _deletedJobIds[schedulerJobId] = 0;
        return Task.FromResult(true);
    }
}
