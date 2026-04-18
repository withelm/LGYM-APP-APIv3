using Hangfire;

namespace LgymApi.Infrastructure.Services;

public sealed class HangfireJobStateReader : IHangfireJobStateReader
{
    private const string FailedStateName = "Failed";
    private const string DeletedStateName = "Deleted";

    public Task<HangfireJobStateSnapshot> ReadAsync(string schedulerJobId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schedulerJobId);

        var jobStorage = JobStorage.Current
            ?? throw new InvalidOperationException("Hangfire job storage is not available for recoverability inspection.");

        var monitoringApi = jobStorage.GetMonitoringApi();
        var details = monitoringApi.JobDetails(schedulerJobId);

        using var connection = jobStorage.GetConnection();
        var jobData = connection.GetJobData(schedulerJobId);
        var stateData = connection.GetStateData(schedulerJobId);

        if (jobData == null && details == null)
        {
            return Task.FromResult(HangfireJobStateSnapshot.Missing());
        }

        var stateName = stateData?.Name
            ?? jobData?.State
            ?? details?.History?.FirstOrDefault()?.StateName;

        if (string.Equals(stateName, FailedStateName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(stateName, DeletedStateName, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(HangfireJobStateSnapshot.Broken(stateName));
        }

        return Task.FromResult(HangfireJobStateSnapshot.Active(stateName));
    }
}
