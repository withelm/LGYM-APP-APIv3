using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.Identity.Contracts.AccountDeletion;

public interface IAccountDeletionRetention
{
    Task<IReadOnlyList<Id<AccountReference>>> GetProtectedAccountIdsAsync(CancellationToken cancellationToken = default);
    Task EnsureEligibleAsync(IReadOnlyCollection<Id<AccountReference>> accountIds, CancellationToken cancellationToken = default);
}
