namespace LgymApi.BackgroundWorker.Common.Jobs;

public interface ICommittedIntentDispatchJob
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
