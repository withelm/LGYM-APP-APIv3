using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common.Jobs;

namespace LgymApi.BackgroundWorker.Jobs;

public sealed class CommittedIntentDispatchJob : ICommittedIntentDispatchJob
{
    private readonly ICommittedIntentDispatcher _committedIntentDispatcher;

    public CommittedIntentDispatchJob(ICommittedIntentDispatcher committedIntentDispatcher)
    {
        _committedIntentDispatcher = committedIntentDispatcher;
    }

    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return _committedIntentDispatcher.DispatchCommittedIntentsAsync(cancellationToken);
    }
}
