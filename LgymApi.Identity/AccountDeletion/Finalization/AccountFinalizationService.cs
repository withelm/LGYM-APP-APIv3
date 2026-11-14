using LgymApi.Application.Repositories;

namespace LgymApi.Identity.AccountDeletion.Finalization;

internal sealed class AccountFinalizationService(
    IAccountFinalizationStore store,
    IAccountFinalizationPhotoManifest photos,
    IAccountFinalizationCleanup cleanup,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IAccountFinalizationService
{
    private const int BatchSize = 100;

    public async Task ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        var candidates = await store.FindDueAsync(timeProvider.GetUtcNow(), BatchSize, cancellationToken);
        var hasFailures = false;
        foreach (var candidate in candidates)
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                if (!await store.LockAndBeginAsync(candidate, timeProvider.GetUtcNow(), cancellationToken))
                    continue;

                await unitOfWork.SaveChangesAsync(cancellationToken);
                await photos.StageAsync(candidate, cancellationToken);
                await cleanup.StageAsync(candidate, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await cleanup.StageRelationshipHistoryAsync(candidate, cancellationToken);
                await store.StageCompletedAsync(candidate, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                await transaction.RollbackAsync(cancellationToken);
                store.DiscardUncommittedChanges();
                hasFailures = true;
            }
        }
        if (hasFailures) throw new InvalidOperationException("Account finalization failed; due work is retained for retry.");
    }
}
