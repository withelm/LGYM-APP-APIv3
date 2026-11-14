using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Repositories;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.Reporting.AccountDeletion;

internal sealed class AccountPhotoDeletionService(
    IPhotoDeletionPersistence persistence,
    IPhotoStorageProvider storage,
    IPhotoStorageInventory inventory,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : IAccountPhotoDeletionService
{
    public Task StageAsync(Id<AccountReference> accountId, Id<CorrelationScope> generation, CancellationToken cancellationToken = default)
        => persistence.StageAccountAsync(accountId, generation, cancellationToken);

    public async Task ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var candidates = await persistence.GetPendingAsync(clock.GetUtcNow(), cancellationToken);
        var hasProviderFailures = false;
        foreach (var candidate in candidates)
        {
            var providerCompleted = false;
            try
            {
                if (candidate.IsPrefix)
                {
                    var page = await inventory.ListAsync(candidate.StorageKey, candidate.ContinuationToken, cancellationToken);
                    providerCompleted = true;
                    await persistence.StagePageAsync(candidate, page, cancellationToken);
                    if (page.ContinuationToken is null)
                    {
                        await persistence.CompleteAsync(candidate.Id, clock.GetUtcNow(), cancellationToken);
                    }
                }
                else
                {
                    await storage.DeleteAsync(candidate.StorageKey, cancellationToken);
                    providerCompleted = true;
                    await persistence.CompleteAsync(candidate.Id, clock.GetUtcNow(), cancellationToken);
                }
            }
            catch (Exception) when (!providerCompleted && !cancellationToken.IsCancellationRequested)
            {
                hasProviderFailures = true;
                await persistence.DeferAsync(candidate.Id, clock.GetUtcNow().AddMinutes(5), cancellationToken);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        if (hasProviderFailures)
            throw new InvalidOperationException("Photo provider operations failed; pending work is retained for retry.");
    }
}
