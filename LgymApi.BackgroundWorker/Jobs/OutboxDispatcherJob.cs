using Hangfire;
using LgymApi.BackgroundWorker.Common.Outbox;

namespace LgymApi.Infrastructure.Jobs;

public sealed class OutboxDispatcherJob : IOutboxDispatcherJob
{
    private readonly IOutboxDispatcher _dispatcher;

    public OutboxDispatcherJob(IOutboxDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [DisableConcurrentExecution(60)]
    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return _dispatcher.DispatchPendingAsync(cancellationToken);
    }
}
