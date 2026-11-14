using LgymApi.Identity.AccountDeletion.Finalization;
using LgymApi.Reporting.AccountDeletion;

namespace LgymApi.Infrastructure.Repositories.AccountDeletion;

public sealed class AccountFinalizationPhotoManifest(IAccountPhotoDeletionService photos) : IAccountFinalizationPhotoManifest
{
    public Task StageAsync(AccountFinalizationCandidate candidate, CancellationToken cancellationToken = default)
        => photos.StageAsync(candidate.AccountId, candidate.Generation, cancellationToken);
}
