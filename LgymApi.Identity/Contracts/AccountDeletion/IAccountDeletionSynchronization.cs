using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.Identity.Contracts.AccountDeletion;

public interface IAccountDeletionSynchronization
{
    Task AcquireAsync(Id<AccountReference> accountId, CancellationToken cancellationToken = default);
}
