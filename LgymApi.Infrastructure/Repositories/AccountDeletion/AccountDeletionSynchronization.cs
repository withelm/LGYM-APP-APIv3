using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.AccountDeletion;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories.AccountDeletion;

public sealed class AccountDeletionSynchronization(AppDbContext database) : IAccountDeletionSynchronization
{
    public async Task AcquireAsync(Id<AccountReference> accountId, CancellationToken cancellationToken = default)
    {
        if (database.Database.IsRelational() && database.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Account lifecycle synchronization requires the caller's transaction.");
        var id = accountId.Rebind<User>();
        await database.Users.IgnoreQueryFilters().Where(user => user.Id == id)
            .StageUpdateAsync(database, user => user.UpdatedAt, user => user.UpdatedAt, cancellationToken);
        var tracked = database.ChangeTracker.Entries<User>().SingleOrDefault(entry => entry.Entity.Id == id);
        if (tracked is not null) await tracked.ReloadAsync(cancellationToken);
    }
}
