using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.Identity.AccountDeletion.Finalization;

public sealed record AccountFinalizationCandidate(Id<AccountReference> AccountId, Id<CorrelationScope> Generation);

public interface IAccountFinalizationStore
{
    Task<IReadOnlyList<AccountFinalizationCandidate>> FindDueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken = default);
    Task<bool> LockAndBeginAsync(AccountFinalizationCandidate candidate, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task StageCompletedAsync(AccountFinalizationCandidate candidate, CancellationToken cancellationToken = default);
    void DiscardUncommittedChanges();
}

public interface IAccountFinalizationCleanup
{
    Task StageAsync(AccountFinalizationCandidate candidate, CancellationToken cancellationToken = default);
    Task StageRelationshipHistoryAsync(AccountFinalizationCandidate candidate, CancellationToken cancellationToken = default);
}

public interface IAccountFinalizationPhotoManifest
{
    Task StageAsync(AccountFinalizationCandidate candidate, CancellationToken cancellationToken = default);
}

public interface IAccountFinalizationService
{
    Task ProcessDueAsync(CancellationToken cancellationToken = default);
}
