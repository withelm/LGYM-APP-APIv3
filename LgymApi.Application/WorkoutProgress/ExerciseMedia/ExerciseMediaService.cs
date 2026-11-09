using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Repositories;
using LgymApi.Application.WorkoutProgress.Persistence;
using LgymApi.Application.WorkoutProgress.Errors;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Authorization;
using LgymApi.Resources;
using Microsoft.Extensions.Logging;

namespace LgymApi.Application.WorkoutProgress.ExerciseMedia;

internal sealed class ExerciseMediaService(
    IExerciseMediaPersistence sessions,
    IWorkoutExercisePersistence exercises,
    IExerciseMediaStorage storage,
    IExerciseMediaVerifier verifier,
    IAuthenticatedAccountAuthorizationService authorization,
    IUnitOfWork unitOfWork,
    ILogger<ExerciseMediaService> logger) : IExerciseMediaService
{
    private const string Publishing = "publishing";
    private const string Bound = "bound";
    private const string Rejected = "rejected";
    private const string Retired = "retired";
    private bool Allowed(AuthenticatedAccountContext? actor) => actor is not null && authorization.HasPermission(actor, AuthConstants.Permissions.ManageGlobalExercises);
    private static Result<T, AppError> Fail<T>(int status, string disposition) => Result<T, AppError>.Failure(new ExerciseMediaOperationError(disposition, status));
    private static Result<ExerciseMediaMutationResult, AppError> Unresolved() => Result<ExerciseMediaMutationResult, AppError>.Failure(new ExerciseMediaRetryError());
    private static Result<ExerciseMediaMutationResult, AppError> Conflict() => Fail<ExerciseMediaMutationResult>(409, ExerciseMediaOperationError.Conflict);
    private static string RequestHash(MutationRequest request) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));

    public Task<Result<ExerciseMediaMutationResult, AppError>> CreateAsync(AuthenticatedAccountContext? actor, CreateExerciseMediaInput input, byte[]? sourceBytes, CancellationToken cancellationToken) =>
        MutateAsync(actor, new MutationRequest { OperationId = input.OperationId, IsCreate = true, Name = input.Name, BodyPart = input.BodyPart, EloFormula = input.EloFormula, Description = input.Description, Source = input.Source }, sourceBytes, cancellationToken);

    public Task<Result<ExerciseMediaMutationResult, AppError>> ChangeAsync(AuthenticatedAccountContext? actor, Id<Exercise> exerciseId, ChangeExerciseMediaInput input, byte[]? sourceBytes, CancellationToken cancellationToken) =>
        MutateAsync(actor, new MutationRequest { OperationId = input.OperationId, ExerciseId = exerciseId, ExpectedImage = input.ExpectedImage, Source = input.Source, Remove = input.Remove, RestoreImage = input.RestoreImage }, sourceBytes, cancellationToken);

    private async Task<Result<ExerciseMediaMutationResult, AppError>> MutateAsync(AuthenticatedAccountContext? actor, MutationRequest request, byte[]? sourceBytes, CancellationToken cancellationToken)
    {
        if (!Allowed(actor)) return Result<ExerciseMediaMutationResult, AppError>.Failure(new ForbiddenError(Messages.Forbidden));
        try { return await ExecuteAsync(actor!, request, sourceBytes, cancellationToken); }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (sessions.IsConcurrencyFailure(exception)) { return Conflict(); }
        catch (Exception exception)
        {
            LogDeferred("mutation", request.OperationId.ToString(), exception);
            return Unresolved();
        }
    }

    private async Task<Result<ExerciseMediaMutationResult, AppError>> ExecuteAsync(AuthenticatedAccountContext actor, MutationRequest request, byte[]? sourceBytes, CancellationToken cancellationToken)
    {
        if (request.OperationId.IsEmpty) return Fail<ExerciseMediaMutationResult>(400, ExerciseMediaOperationError.Unresolved);
        var hash = RequestHash(request);
        var operation = await sessions.FindOperationAsync(actor.Id, request.OperationId, cancellationToken);
        var replay = Replay(operation, hash);
        if (replay.HasValue) return replay.Value;
        if (!ValidIntent(request)) return await RejectMutationAsync(actor, request.OperationId, 400, cancellationToken);
        var target = request.IsCreate ? null : await exercises.FindGlobalByIdAsync(request.ExerciseId, cancellationToken);
        if (!request.IsCreate && target is not { IsDeleted: false }) return Fail<ExerciseMediaMutationResult>(404, ExerciseMediaOperationError.Conflict);
        if (target is not null && !string.Equals(target.Image, request.ExpectedImage, StringComparison.Ordinal)) return Conflict();
        if (request.RestoreImage is not null && !await sessions.WasRetainedAsync(request.ExerciseId, request.RestoreImage, cancellationToken))
            return await RejectMutationAsync(actor, request.OperationId, 400, cancellationToken);
        var prepared = await PrepareAsync(request.Source, operation, sourceBytes, cancellationToken);
        if (prepared.IsFailure)
        {
            if (prepared.Error.HttpStatusCode == 422 && operation is null) return await RejectMutationAsync(actor, request.OperationId, 422, cancellationToken);
            return Result<ExerciseMediaMutationResult, AppError>.Failure(prepared.Error);
        }
        if (operation is null) operation = await ReserveAsync(actor, request, prepared.Value, hash, cancellationToken);
        if (operation.Status == Rejected) return Fail<ExerciseMediaMutationResult>(operation.RejectionStatusCode ?? 400, ExerciseMediaOperationError.Rejected);
        if (operation.ExpiresAt <= DateTimeOffset.UtcNow) return Unresolved();
        return await PublishAndBindAsync(operation, request, prepared.Value, cancellationToken);
    }

    private static bool ValidIntent(MutationRequest request)
    {
        if (request.IsCreate) return !string.IsNullOrWhiteSpace(request.Name) && request.Name.Length <= 200 && request.Description?.Length is not > 16000 && request.BodyPart != BodyParts.Unknown && Enum.IsDefined(request.BodyPart);
        return !request.ExerciseId.IsEmpty && (request.Source is not null ? 1 : 0) + (request.Remove ? 1 : 0) + (request.RestoreImage is not null ? 1 : 0) == 1;
    }

    private static Result<ExerciseMediaMutationResult, AppError>? Replay(ExerciseMediaSession? operation, string hash)
    {
        if (operation is null) return null;
        if (operation.Status is Rejected or Retired) return Fail<ExerciseMediaMutationResult>(operation.RejectionStatusCode ?? 409, ExerciseMediaOperationError.Rejected);
        if (operation.OperationHash != hash) return Conflict();
        if (operation.Status == Bound && operation.TargetExerciseId.HasValue)
            return Result<ExerciseMediaMutationResult, AppError>.Success(new(operation.TargetExerciseId.Value.ToString(), operation.ResultImage));
        return operation.Status == Publishing ? null : Conflict();
    }

    private async Task<Result<VerifiedExerciseMedia?, AppError>> PrepareAsync(ExerciseMediaSourceInput? source, ExerciseMediaSession? operation, byte[]? sourceBytes, CancellationToken cancellationToken)
    {
        if (source is null) return sourceBytes is null ? Result<VerifiedExerciseMedia?, AppError>.Success(null) : Fail<VerifiedExerciseMedia?>(422, ExerciseMediaOperationError.Unresolved);
        if (!storage.IsConfigured) return Fail<VerifiedExerciseMedia?>(503, ExerciseMediaOperationError.Unresolved);
        if (sourceBytes is not null)
        {
            try
            {
                if (sourceBytes.LongLength != source.SizeBytes) throw new ExerciseMediaRejectedException();
                var result = await verifier.VerifyAsync(sourceBytes, source.ContentType, source.SourceSha256, cancellationToken);
                if (operation is not null && operation.PublishedSha256 != result.PublishedSha256) return Fail<VerifiedExerciseMedia?>(409, ExerciseMediaOperationError.Conflict);
                return Result<VerifiedExerciseMedia?, AppError>.Success(result);
            }
            catch (ExerciseMediaRejectedException) { return Fail<VerifiedExerciseMedia?>(operation is null ? 422 : 503, ExerciseMediaOperationError.Unresolved); }
        }
        if (operation?.ObjectKey is null || operation.PublishedSha256 is null) return Fail<VerifiedExerciseMedia?>(428, ExerciseMediaOperationError.SourceRequired);
        var published = await storage.ReadAsync(operation.ObjectKey, cancellationToken);
        if (published is null) return Fail<VerifiedExerciseMedia?>(428, ExerciseMediaOperationError.SourceRequired);
        if (Convert.ToHexStringLower(SHA256.HashData(published)) != operation.PublishedSha256) return Fail<VerifiedExerciseMedia?>(503, ExerciseMediaOperationError.Unresolved);
        return Result<VerifiedExerciseMedia?, AppError>.Success(new(published, operation.ContentType, operation.Extension!, operation.SourceSha256, operation.PublishedSha256, operation.Width, operation.Height, operation.Frames));
    }

    private async Task<ExerciseMediaSession> ReserveAsync(AuthenticatedAccountContext actor, MutationRequest request, VerifiedExerciseMedia? media, string hash, CancellationToken cancellationToken)
    {
        var targetId = request.IsCreate ? Id<Exercise>.New() : request.ExerciseId;
        var operation = NewOperation(actor, request.OperationId);
        operation.OperationHash = hash;
        operation.IsCreate = request.IsCreate;
        operation.TargetExerciseId = targetId;
        operation.PreviousImage = request.ExpectedImage;
        operation.ExpiresAt = DateTimeOffset.UtcNow.AddHours(24);
        CopyVerifiedState(operation, media, request.Source);
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        if (media is not null)
        {
            var filename = media.Extension == "gif" ? "animation.gif" : $"image.{media.Extension}";
            operation.ObjectKey = $"exercises/{targetId}/{media.PublishedSha256}/{filename}";
            var keyFence = await sessions.FindObjectAsync(operation.ObjectKey, cancellationToken);
            if (keyFence is { IsRetired: true })
            {
                operation.Status = Rejected;
                operation.RejectionStatusCode = 409;
            }
            else if (keyFence is null)
                await sessions.AddObjectAsync(new() { Id = Id<ExerciseMediaObjectFence>.New(), ExerciseId = targetId, ObjectKey = operation.ObjectKey, PublicUrl = storage.GetPublicUrl(operation.ObjectKey) }, cancellationToken);
            else await sessions.SaveObjectRevisionAsync(keyFence, cancellationToken);
        }
        await sessions.AddAsync(operation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return operation;
    }

    private static void CopyVerifiedState(ExerciseMediaSession operation, VerifiedExerciseMedia? media, ExerciseMediaSourceInput? source)
    {
        operation.ContentType = media?.ContentType ?? string.Empty;
        operation.SizeBytes = source?.SizeBytes ?? 0;
        operation.SourceSha256 = media?.SourceSha256 ?? string.Empty;
        operation.PublishedSha256 = media?.PublishedSha256;
        operation.Extension = media?.Extension;
        operation.Width = media?.Width ?? 0;
        operation.Height = media?.Height ?? 0;
        operation.Frames = media?.Frames ?? 0;
    }

    private async Task<Result<ExerciseMediaMutationResult, AppError>> PublishAndBindAsync(ExerciseMediaSession operation, MutationRequest request, VerifiedExerciseMedia? media, CancellationToken cancellationToken)
    {
        ExerciseMediaObjectState? keyFence = null;
        var image = request.RestoreImage;
        if (operation.ObjectKey is not null)
        {
            keyFence = await sessions.FindObjectAsync(operation.ObjectKey, cancellationToken);
            if (keyFence is null || keyFence.IsRetired || media is null) return Conflict();
            image = await storage.PublishAsync(operation.ObjectKey, media.Bytes, media.ContentType, cancellationToken);
        }
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        operation.Status = Bound;
        operation.ResultImage = image;
        await sessions.SaveSessionRevisionAsync(operation, cancellationToken);
        if (keyFence is not null) await sessions.SaveObjectRevisionAsync(keyFence, cancellationToken);
        var id = operation.TargetExerciseId!.Value;
        if (request.IsCreate)
            await exercises.AddAsync(new(id, null, request.Name.Trim(), request.BodyPart, request.EloFormula ?? ExerciseEloFormula.Standard, request.Description, image, false), cancellationToken);
        else await sessions.StageImageAsync(id, request.ExpectedImage, image, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<ExerciseMediaMutationResult, AppError>.Success(new(id.ToString(), image));
    }

    public async Task<bool> RejectAsync(AuthenticatedAccountContext? actor, Id<ExerciseMediaOperationReference> operationId, int statusCode, CancellationToken cancellationToken)
    {
        if (!Allowed(actor) || operationId.IsEmpty) return false;
        try
        {
            var operation = await sessions.FindOperationAsync(actor!.Id, operationId, cancellationToken);
            if (operation is not null) return operation.Status is Rejected or Retired;
            operation = NewOperation(actor, operationId);
            operation.Status = Rejected;
            operation.RejectionStatusCode = statusCode;
            await sessions.AddAsync(operation, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            LogDeferred("reject", operationId.ToString(), exception);
            return false;
        }
    }

    private async Task<Result<ExerciseMediaMutationResult, AppError>> RejectMutationAsync(AuthenticatedAccountContext actor, Id<ExerciseMediaOperationReference> operationId, int statusCode, CancellationToken cancellationToken) =>
        await RejectAsync(actor, operationId, statusCode, cancellationToken) ? Fail<ExerciseMediaMutationResult>(statusCode, ExerciseMediaOperationError.Rejected) : Unresolved();

    private static ExerciseMediaSession NewOperation(AuthenticatedAccountContext actor, Id<ExerciseMediaOperationReference> operationId) =>
        new() { Id = Id<ExerciseMediaUpload>.New(), ActorId = actor.Id, OperationId = operationId, Status = Publishing };

    public async Task<Result<ExerciseMediaCleanupResult, AppError>> CleanupAsync(AuthenticatedAccountContext? actor, CancellationToken cancellationToken)
    {
        if (!Allowed(actor)) return Result<ExerciseMediaCleanupResult, AppError>.Failure(new ForbiddenError(Messages.Forbidden));
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
                // A failed commit can leave this request's tracked state uncertain. Retry in a fresh scope.
                break;
            }
        }
        return Result<ExerciseMediaCleanupResult, AppError>.Success(new(cleaned, candidates.Count - cleaned));
    }

    private async Task<ExerciseMediaObjectState?> ClaimOrphanAsync(string objectKey, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var keyFence = await sessions.FindObjectAsync(objectKey, cancellationToken);
        if (keyFence is null) return null;
        var linked = await sessions.SessionsForObjectAsync(objectKey, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (await sessions.IsImageReferencedAsync(keyFence.PublicUrl, cancellationToken)
            || linked.Any(x => x.Status == Bound || x.Status is not (Publishing or Rejected or Retired) || x.Status == Publishing && x.ExpiresAt > now)) return null;
        foreach (var operation in linked.Where(x => x.Status is not (Bound or Rejected or Retired)))
        {
            operation.Status = Retired;
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

    private void LogDeferred(string phase, string reference, Exception exception) => logger.LogWarning("Exercise media deferred: phase={Phase}, reference={Reference}, category={Category}", phase, reference, exception.GetType().Name);

    private sealed record MutationRequest
    {
        public Id<ExerciseMediaOperationReference> OperationId { get; init; }
        public bool IsCreate { get; init; }
        public Id<Exercise> ExerciseId { get; init; }
        public string Name { get; init; } = string.Empty;
        public BodyParts BodyPart { get; init; }
        public ExerciseEloFormula? EloFormula { get; init; }
        public string? Description { get; init; }
        public string? ExpectedImage { get; init; }
        public ExerciseMediaSourceInput? Source { get; init; }
        public bool Remove { get; init; }
        public string? RestoreImage { get; init; }
    }
}
