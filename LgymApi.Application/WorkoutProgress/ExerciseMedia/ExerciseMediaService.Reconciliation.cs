using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.WorkoutProgress.Errors;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Resources;

namespace LgymApi.Application.WorkoutProgress.ExerciseMedia;

internal sealed partial class ExerciseMediaService
{
    private async Task<Result<ExerciseMediaMutationResult, AppError>> ResolveExpiredAsync(ExerciseMediaSession operation, CancellationToken cancellationToken)
    {
        operation.Status = Rejected;
        operation.RejectionStatusCode = 409;
        await sessions.SaveSessionRevisionAsync(operation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        // The same revision fences every older completion, even without an object/deletable key.
        return Fail<ExerciseMediaMutationResult>(409, ExerciseMediaOperationError.Rejected);
    }

    public async Task<Result<ExerciseMediaCleanupResult, AppError>> CleanupAsync(AuthenticatedAccountContext? actor, CancellationToken cancellationToken)
    {
        if (!Allowed(actor)) return Result<ExerciseMediaCleanupResult, AppError>.Failure(new ForbiddenError(Messages.Forbidden));
        await ResolveExpiredBatchAsync(cancellationToken);
        var candidates = await sessions.CleanupCandidatesAsync(DateTimeOffset.UtcNow, cancellationToken);
        var cleaned = 0;
        foreach (var candidate in candidates)
        {
            try
            {
                var claimed = await ClaimOrphanAsync(candidate.ObjectKey, cancellationToken);
                if (claimed is null) continue;
                // No storage call occurs before the transaction's atomic key/operation fence commits.
                await storage.DeleteAsync(claimed.ObjectKey, cancellationToken);
                claimed.LastDeleteSucceeded = true;
                await sessions.SaveObjectRevisionAsync(claimed, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                cleaned++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                LogDeferred("cleanup", candidate.Id.ToString(), exception);
                // A later candidate must reload fresh state after a lost or uncertain commit.
                sessions.DiscardTrackedState();
            }
        }
        return Result<ExerciseMediaCleanupResult, AppError>.Success(new(cleaned, candidates.Count - cleaned));
    }

    private async Task ResolveExpiredBatchAsync(CancellationToken cancellationToken)
    {
        foreach (var candidate in await sessions.ExpiredOperationsAsync(DateTimeOffset.UtcNow, cancellationToken))
        {
            try
            {
                var current = await sessions.FindOperationAsync(candidate.ActorId, candidate.OperationId, cancellationToken);
                if (current is { Status: Publishing } && current.ExpiresAt <= DateTimeOffset.UtcNow)
                    await ResolveExpiredAsync(current, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                LogDeferred("expiry", candidate.Id.ToString(), exception);
                sessions.DiscardTrackedState();
            }
        }
    }

    private async Task<ExerciseMediaObjectState?> ClaimOrphanAsync(string objectKey, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var keyFence = await sessions.FindObjectAsync(objectKey, cancellationToken);
        if (keyFence is null) return null;
        var linked = await sessions.SessionsForObjectAsync(objectKey, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (await sessions.IsImageReferencedAsync(keyFence.PublicUrl, cancellationToken)
            || linked.Any(x => x.Status == Bound || x.Status is not (Publishing or Rejected) || x.Status == Publishing && x.ExpiresAt > now)) return null;
        foreach (var operation in linked.Where(x => x.Status is not (Bound or Rejected)))
        {
            operation.Status = Rejected;
            operation.RejectionStatusCode = 409;
            await sessions.SaveSessionRevisionAsync(operation, cancellationToken);
        }
        // Permanent retirement is essential: a timed-out DELETE may complete after a later retry.
        keyFence.IsRetired = true;
        keyFence.LastCleanupAt = now;
        keyFence.LastDeleteSucceeded = false;
        await sessions.SaveObjectRevisionAsync(keyFence, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return keyFence;
    }
}
