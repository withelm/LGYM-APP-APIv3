using System.Collections.Concurrent;

namespace LgymApi.Infrastructure.Services;

public sealed class InMemoryHangfireJobStateReader : IHangfireJobStateReader
{
    private readonly ConcurrentDictionary<string, HangfireJobStateSnapshot> _states = new(StringComparer.Ordinal);

    public Task<HangfireJobStateSnapshot> ReadAsync(string schedulerJobId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schedulerJobId);

        var snapshot = _states.TryGetValue(schedulerJobId, out var state)
            ? state
            : HangfireJobStateSnapshot.Missing();

        return Task.FromResult(snapshot);
    }

    public void SetActive(string schedulerJobId, string stateName)
    {
        _states[schedulerJobId] = HangfireJobStateSnapshot.Active(stateName);
    }

    public void SetBroken(string schedulerJobId, string stateName)
    {
        _states[schedulerJobId] = HangfireJobStateSnapshot.Broken(stateName);
    }

    public void SetMissing(string schedulerJobId)
    {
        _states[schedulerJobId] = HangfireJobStateSnapshot.Missing();
    }

    public void Reset()
    {
        _states.Clear();
    }
}
