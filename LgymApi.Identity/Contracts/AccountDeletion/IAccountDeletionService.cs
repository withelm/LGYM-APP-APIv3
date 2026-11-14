using LgymApi.Domain.ValueObjects;

namespace LgymApi.Identity.Contracts.AccountDeletion;

public interface IAccountDeletionService
{
    Task RequestRecoveryEmailAsync(string email, CancellationToken cancellationToken = default);
    Task RequestAsync(Id<AccountReference> accountId, CancellationToken cancellationToken = default);
    Task CancelAsync(string recoveryToken, CancellationToken cancellationToken = default);
    Task LogoutRecoveryAsync(string recoveryToken, CancellationToken cancellationToken = default);
    Task<string> SignInWithPasswordAsync(string name, string password, CancellationToken cancellationToken = default);
}
