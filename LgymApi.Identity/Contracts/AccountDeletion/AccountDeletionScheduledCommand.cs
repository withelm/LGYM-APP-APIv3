using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Identity.Contracts.AccountDeletion;

public sealed record AccountDeletionScheduledCommand(
    Id<AccountReference> AccountId,
    Id<CorrelationScope> DeletionGeneration) : IActionCommand;

public interface IAccountDeletionScheduleConfirmation
{
    Task ExecuteAsync(AccountDeletionScheduledCommand command, CancellationToken cancellationToken = default);
}
