using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.AccountDeletion;
using LgymApi.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Identity.AccountDeletion;

internal sealed class AccountDeletionRetention(IIdentityPersistenceContext context) : IAccountDeletionRetention
{
    public async Task EnsureEligibleAsync(IReadOnlyCollection<Id<AccountReference>> accountIds, CancellationToken cancellationToken = default)
    {
        var ids = accountIds.Select(id => id.Rebind<LgymApi.Domain.Entities.User>()).ToArray();
        var accounts = await context.Users.IgnoreQueryFilters().Where(user => ids.Contains(user.Id)).ToListAsync(cancellationToken);
        if (accounts.Any(user => user.DeletionState is AccountDeletionState.PendingDeletion or AccountDeletionState.Finalizing))
        {
            throw new InvalidOperationException("Retention selection crossed an account recovery boundary; retry with a fresh scope.");
        }
    }

    public async Task<IReadOnlyList<Id<AccountReference>>> GetProtectedAccountIdsAsync(CancellationToken cancellationToken = default)
    {
        var ids = await context.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.DeletionState == AccountDeletionState.PendingDeletion
                || user.DeletionState == AccountDeletionState.Finalizing)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
        return ids.Select(id => id.Rebind<AccountReference>()).ToArray();
    }
}
