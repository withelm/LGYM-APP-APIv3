using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.AccountDeletion.Finalization;
using LgymApi.Identity.Contracts;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories.AccountDeletion;

public sealed class AccountFinalizationStore(AppDbContext db) : IAccountFinalizationStore
{
    public void DiscardUncommittedChanges() => db.ChangeTracker.Clear();

    public async Task<IReadOnlyList<AccountFinalizationCandidate>> FindDueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken = default)
    {
        var rows = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.DeletionGeneration != null &&
                (user.DeletionState == AccountDeletionState.Finalizing ||
                 (user.DeletionState == AccountDeletionState.PendingDeletion && user.DeletionEffectiveAt <= now)))
            .OrderBy(user => user.DeletionEffectiveAt).ThenBy(user => user.Id)
            .Take(limit).Select(user => new { user.Id, user.DeletionGeneration })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new AccountFinalizationCandidate(row.Id.Rebind<AccountReference>(), row.DeletionGeneration.GetValueOrDefault())).ToArray();
    }

    public async Task<bool> LockAndBeginAsync(AccountFinalizationCandidate candidate, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsRelational() && db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Account finalization requires a caller-owned transaction.");

        var id = candidate.AccountId.Rebind<User>();
        await db.Users.IgnoreQueryFilters().Where(user => user.Id == id)
            .StageUpdateAsync(db, user => user.UpdatedAt, user => user.UpdatedAt, cancellationToken);
        var tracked = db.ChangeTracker.Entries<User>().FirstOrDefault(entry => entry.Entity.Id == id);
        if (tracked is not null)
            await tracked.ReloadAsync(cancellationToken);
        var user = await db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(user => user.Id == id, cancellationToken);
        if (user is null || user.DeletionGeneration != candidate.Generation ||
            (user.DeletionState != AccountDeletionState.Finalizing &&
             (user.DeletionState != AccountDeletionState.PendingDeletion || user.DeletionEffectiveAt is null || user.DeletionEffectiveAt > now)))
            return false;

        user.DeletionState = AccountDeletionState.Finalizing;
        user.LifecycleRevision = Id<CorrelationScope>.New();
        return true;
    }

    public async Task StageCompletedAsync(AccountFinalizationCandidate candidate, CancellationToken cancellationToken = default)
    {
        var id = candidate.AccountId.Rebind<User>();
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(user => user.Id == id, cancellationToken);
        var random = Id<CorrelationScope>.New().ToString();
        user.Name = $"deleted_{random}";
        user.Email = new Email($"deleted_{random}@invalid.example");
        user.Avatar = null;
        user.PreferredLanguage = string.Empty;
        user.PreferredTimeZone = string.Empty;
        user.ProfileRank = string.Empty;
        user.PlanId = null;
        user.LegacyHash = null;
        user.LegacySalt = null;
        user.LegacyDigest = null;
        user.LegacyIterations = null;
        user.LegacyKeyLength = null;
        user.IsVisibleInRanking = false;
        user.IsBlocked = true;
        user.IsDeleted = true;
        user.DeletionRequestedAt = null;
        user.DeletionEffectiveAt = null;
        user.DeletionGeneration = null;
        user.DeletionState = AccountDeletionState.PermanentlyDeleted;
        user.LifecycleRevision = Id<CorrelationScope>.New();
    }
}
