using Hangfire;
using LgymApi.Reporting.AccountDeletion;

namespace LgymApi.BackgroundWorker.Jobs;

public sealed class AccountPhotoDeletionJob(IAccountPhotoDeletionService photos)
{
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public Task ExecuteAsync(CancellationToken cancellationToken = default)
        => photos.ProcessPendingAsync(cancellationToken);
}
