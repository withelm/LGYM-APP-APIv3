using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.Reporting.AccountDeletion;

public sealed record PhotoDeletionCandidate(
    Id<PhotoDeletionWorkItem> Id, Id<CorrelationScope> Generation, string StorageKey, bool IsPrefix, string? ContinuationToken);

public interface IPhotoDeletionPersistence
{
    Task StageAccountAsync(Id<AccountReference> accountId, Id<CorrelationScope> generation, CancellationToken cancellationToken);
    Task<IReadOnlyList<PhotoDeletionCandidate>> GetPendingAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task StagePageAsync(PhotoDeletionCandidate prefix, PhotoStoragePage page, CancellationToken cancellationToken);
    Task CompleteAsync(Id<PhotoDeletionWorkItem> id, DateTimeOffset now, CancellationToken cancellationToken);
    Task DeferAsync(Id<PhotoDeletionWorkItem> id, DateTimeOffset retryAt, CancellationToken cancellationToken);
}

public sealed record PhotoStoragePage(IReadOnlyList<string> Keys, string? ContinuationToken);

public interface IPhotoStorageInventory
{
    Task<PhotoStoragePage> ListAsync(string prefix, string? continuationToken, CancellationToken cancellationToken);
}
