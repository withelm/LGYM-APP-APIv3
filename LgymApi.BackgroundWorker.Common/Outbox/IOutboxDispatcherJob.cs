namespace LgymApi.BackgroundWorker.Common.Outbox;

public interface IOutboxDispatcherJob
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
