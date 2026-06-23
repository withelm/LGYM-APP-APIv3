namespace LgymApi.BackgroundWorker.Common.Jobs;

public interface IRecurringReportAssignmentProcessingJob
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
