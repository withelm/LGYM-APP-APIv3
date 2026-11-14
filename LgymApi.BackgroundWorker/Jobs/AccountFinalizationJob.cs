using Hangfire;
using LgymApi.Identity.AccountDeletion.Finalization;

namespace LgymApi.BackgroundWorker.Jobs;

public sealed class AccountFinalizationJob(IAccountFinalizationService finalization)
{
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await finalization.ProcessDueAsync(cancellationToken);
    }
}
