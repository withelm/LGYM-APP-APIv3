using LgymApi.BackgroundWorker.Actions.Contracts;
using LgymApi.Identity.Contracts.AccountDeletion;

namespace LgymApi.BackgroundWorker.Actions;

public sealed class AccountDeletionScheduledCommandHandler(IAccountDeletionScheduleConfirmation confirmation)
    : IBackgroundAction<AccountDeletionScheduledCommand>
{
    public Task ExecuteAsync(AccountDeletionScheduledCommand command, CancellationToken cancellationToken = default)
        => confirmation.ExecuteAsync(command, cancellationToken);
}
