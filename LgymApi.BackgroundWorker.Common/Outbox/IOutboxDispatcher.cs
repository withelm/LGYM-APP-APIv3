namespace LgymApi.BackgroundWorker.Common.Outbox;

public interface IOutboxDispatcher
{
    Task DispatchPendingAsync(CancellationToken cancellationToken = default);
}
