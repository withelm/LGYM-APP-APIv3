using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.Identity.Contracts.AccountDeletion;

public sealed record AccountRecoveryEmail(
    Id<AccountReference> AccountId, Id<CorrelationScope> CorrelationId,
    string RecipientEmail, string RecoveryToken, string CultureName);

public interface IAccountRecoveryEmailOutbox
{
    Task StageAsync(AccountRecoveryEmail message, CancellationToken cancellationToken = default);
}
