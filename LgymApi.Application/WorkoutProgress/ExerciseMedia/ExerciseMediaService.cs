using System.Security.Cryptography;
using System.Text;
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
    private bool Allowed(AuthenticatedAccountContext? actor) => actor is not null && authorization.HasPermission(actor, AuthConstants.Permissions.ManageGlobalExercises);
    private static Result<T, AppError> Invalid<T>() => Result<T, AppError>.Failure(new BadRequestError(Messages.ExerciseMediaInvalid));
    private static Result<T, AppError> Forbidden<T>() => Result<T, AppError>.Failure(new ForbiddenError(Messages.Forbidden));
    private static Result<T, AppError> Conflict<T>() => Result<T, AppError>.Failure(new ConflictError(Messages.ExerciseMediaConflict));
    private static Result<T, AppError> Expired<T>() => Result<T, AppError>.Failure(new ExerciseMediaExpiredError());
    private Result<T, AppError> Retry<T>(Exception? exception = null, string phase = "availability", string? reference = null)
    {
        // Provider messages may include bearer URLs. Log allowlisted identifiers and type names only.
        logger.LogWarning("Exercise media retry required: phase={Phase}, reference={Reference}, category={Category}", phase, reference, exception?.GetType().Name ?? "Unavailable");
        return Result<T, AppError>.Failure(new ExerciseMediaRetryError());
    }
    private static LgymApi.Domain.ValueObjects.Id<LgymApi.Identity.Contracts.AccountReference> ActorId(AuthenticatedAccountContext actor) => actor.Id;
    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static string MutationHash(string kind, params object?[] values) => Hash(System.Text.Json.JsonSerializer.Serialize(new { kind, values }));

    public async Task<Result<ExerciseMediaUploadResult, AppError>> InitiateAsync(AuthenticatedAccountContext? actor, InitiateExerciseMediaInput input, CancellationToken cancellationToken)
    {
        if (!Allowed(actor)) return Forbidden<ExerciseMediaUploadResult>();
        if (!storage.IsConfigured) return Retry<ExerciseMediaUploadResult>();
        if (!ExerciseMediaLimits.ContentTypes.Contains(input.ContentType) || input.SizeBytes is <= 0 or > ExerciseMediaLimits.MaxBytes || string.IsNullOrEmpty(input.Sha256) || input.Sha256.Length != 64 || !input.Sha256.All(Uri.IsHexDigit)) return Invalid<ExerciseMediaUploadResult>();
        if (input.ExerciseId.HasValue && await exercises.FindGlobalByIdAsync(input.ExerciseId.Value, cancellationToken) is not { IsDeleted: false }) return Result<ExerciseMediaUploadResult, AppError>.Failure(new NotFoundError(Messages.DidntFind));
        if (await sessions.CountRecentAsync(ActorId(actor!), DateTimeOffset.UtcNow.AddMinutes(-10), cancellationToken) >= 20) return Invalid<ExerciseMediaUploadResult>();
        var session = new ExerciseMediaSession { Id = Id<ExerciseMediaUpload>.New(), ActorId = ActorId(actor!), TargetExerciseId = input.ExerciseId, ContentType = input.ContentType, SizeBytes = input.SizeBytes, SourceSha256 = input.Sha256.ToLowerInvariant(), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15) };
        try
        {
            var expires = DateTimeOffset.UtcNow.AddMinutes(5);
            var url = await storage.SignUploadAsync(ExerciseMediaLimits.StagingKey(session.Id), input.ContentType, expires, cancellationToken);
            await sessions.AddAsync(session, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ExerciseMediaUploadResult, AppError>.Success(new(session.Id.ToString(), url, expires, new() { ["Content-Type"] = input.ContentType }));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) { return Retry<ExerciseMediaUploadResult>(exception, "initiate", session.Id.ToString()); }
    }

    public async Task<Result<ExerciseMediaVerificationResult, AppError>> VerifyAsync(AuthenticatedAccountContext? actor, Id<ExerciseMediaUpload> reference, CancellationToken cancellationToken)
    {
        if (!Allowed(actor)) return Forbidden<ExerciseMediaVerificationResult>();
        var session = await sessions.FindAsync(reference, cancellationToken);
        if (session is null || session.ActorId != ActorId(actor!)) return Invalid<ExerciseMediaVerificationResult>();
        if (session.Status == "expired") return Expired<ExerciseMediaVerificationResult>();
        if (session.Status == "bound" || session.Status == "verified") return Verification(session);
        if (session.Status == "rejected") return Result<ExerciseMediaVerificationResult, AppError>.Failure(new UnprocessableEntityError(Messages.ExerciseMediaInvalid));
        if (session.Status != "pending") return Invalid<ExerciseMediaVerificationResult>();
        if (session.ExpiresAt <= DateTimeOffset.UtcNow) return Expired<ExerciseMediaVerificationResult>();
        if (session.TargetExerciseId.HasValue && await exercises.FindGlobalByIdAsync(session.TargetExerciseId.Value, cancellationToken) is not { IsDeleted: false }) return Invalid<ExerciseMediaVerificationResult>();
        try
        {
            var bytes = await storage.ReadPrivateAsync(ExerciseMediaLimits.StagingKey(session.Id), cancellationToken);
            if (bytes.LongLength != session.SizeBytes) throw new ExerciseMediaRejectedException();
            var verified = await verifier.VerifyAsync(bytes, session.ContentType, session.SourceSha256, cancellationToken);
            session.PublishedSha256 = verified.PublishedSha256;
            session.Extension = verified.Extension;
            session.Width = verified.Width;
            session.Height = verified.Height;
            session.Frames = verified.Frames;
            // This key is never signed for clients. Publication reads only these frozen bytes.
            await storage.WriteVerifiedAsync(ExerciseMediaLimits.VerifiedKey(session), verified.Bytes, verified.ContentType, cancellationToken);
            session.Status = "verified";
            await sessions.SaveSessionRevisionAsync(session, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Verification(session);
        }
        catch (ExerciseMediaRejectedException)
        {
            try
            {
                session.Status = "rejected";
                await sessions.SaveSessionRevisionAsync(session, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return Result<ExerciseMediaVerificationResult, AppError>.Failure(new UnprocessableEntityError(Messages.ExerciseMediaInvalid));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) when (sessions.IsConcurrencyFailure(exception)) { return Conflict<ExerciseMediaVerificationResult>(); }
            catch (Exception exception) { return Retry<ExerciseMediaVerificationResult>(exception, "reject", session.Id.ToString()); }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (sessions.IsConcurrencyFailure(ex)) { return Conflict<ExerciseMediaVerificationResult>(); }
        catch (Exception exception) { return Retry<ExerciseMediaVerificationResult>(exception, "verify", session.Id.ToString()); }
    }

    private static Result<ExerciseMediaVerificationResult, AppError> Verification(ExerciseMediaSession session) => Result<ExerciseMediaVerificationResult, AppError>.Success(new(session.Id.ToString(), "verified", session.SourceSha256, session.PublishedSha256!, session.ContentType, session.Width, session.Height, session.Frames));

    public async Task<Result<Unit, AppError>> CancelAsync(AuthenticatedAccountContext? actor, Id<ExerciseMediaUpload> reference, CancellationToken cancellationToken)
    {
        if (!Allowed(actor)) return Forbidden<Unit>();
        var session = await sessions.FindAsync(reference, cancellationToken);
        if (session is null || session.ActorId != ActorId(actor!)) return Invalid<Unit>();
        if (session.Status == "bound") return Conflict<Unit>();
        try
        {
            session.Status = "cancelled";
            await sessions.SaveSessionRevisionAsync(session, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            // Deferred cleanup follows PUT expiry; bucket lifecycle also covers an already in-flight late PUT.
            return Result<Unit, AppError>.Success(Unit.Value);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (sessions.IsConcurrencyFailure(exception)) { return Conflict<Unit>(); }
        catch (Exception exception) { return Retry<Unit>(exception, "cancel", session.Id.ToString()); }
    }

    public async Task<Result<ExerciseMediaMutationResult, AppError>> CreateAsync(AuthenticatedAccountContext? actor, CreateExerciseMediaInput input, CancellationToken cancellationToken)
    {
        if (!Allowed(actor)) return Forbidden<ExerciseMediaMutationResult>();
        if (input.OperationId == Id<ExerciseMediaOperationReference>.Empty || string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 200 || input.BodyPart == BodyParts.Unknown || !Enum.IsDefined(input.BodyPart) || (input.EloFormula.HasValue && !Enum.IsDefined(input.EloFormula.Value))) return Invalid<ExerciseMediaMutationResult>();
        var hash = MutationHash("create", input.Name, input.BodyPart, input.EloFormula, input.Description, input.UploadReference?.ToString());
        var previous = await sessions.FindOperationAsync(ActorId(actor!), input.OperationId, cancellationToken);
        if (previous is not null && (previous.Status == "bound" || previous.OperationHash != hash)) return Replay(previous, hash);
        var session = previous is not null ? await sessions.FindAsync(previous.Id, cancellationToken) : input.UploadReference.HasValue ? await sessions.FindAsync(input.UploadReference.Value, cancellationToken) : null;
        if (previous is null && input.UploadReference.HasValue && !Usable(session, actor!, null)) return IsExpired(session, actor!, null) ? Expired<ExerciseMediaMutationResult>() : Invalid<ExerciseMediaMutationResult>();
        if (session is not null && session.ExpiresAt <= DateTimeOffset.UtcNow) return session.Status == "publishing" ? Retry<ExerciseMediaMutationResult>() : Expired<ExerciseMediaMutationResult>();
        var id = previous?.TargetExerciseId ?? Id<Exercise>.New();
        try
        {
            if (previous is null)
            {
                session ??= new ExerciseMediaSession { Id = Id<ExerciseMediaUpload>.New(), ActorId = ActorId(actor!) };
                if (!input.UploadReference.HasValue) await sessions.AddAsync(session, cancellationToken);
                Reserve(session, input.OperationId, hash, id, null);
                await sessions.SaveSessionRevisionAsync(session, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            if (session is null || session.Status != "publishing") return Conflict<ExerciseMediaMutationResult>();
            var image = input.UploadReference.HasValue ? await PublishAsync(session, id, cancellationToken) : null;
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            Bind(session, image);
            await sessions.SaveSessionRevisionAsync(session, cancellationToken);
            await exercises.AddAsync(new(id, null, input.Name.Trim(), input.BodyPart, input.EloFormula ?? ExerciseEloFormula.Standard, input.Description, image, false), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<ExerciseMediaMutationResult, AppError>.Success(new(id.ToString(), image));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (sessions.IsConcurrencyFailure(ex)) { return Conflict<ExerciseMediaMutationResult>(); }
        catch (Exception exception) { return Retry<ExerciseMediaMutationResult>(exception, "create", input.OperationId.ToString()); }
    }

    public async Task<Result<ExerciseMediaMutationResult, AppError>> ChangeAsync(AuthenticatedAccountContext? actor, Id<Exercise> exerciseId, ChangeExerciseMediaInput input, CancellationToken cancellationToken)
    {
        if (!Allowed(actor)) return Forbidden<ExerciseMediaMutationResult>();
        var choices = (input.UploadReference.HasValue ? 1 : 0) + (input.Remove ? 1 : 0) + (input.RestoreImage is not null ? 1 : 0);
        if (input.OperationId == Id<ExerciseMediaOperationReference>.Empty || exerciseId.IsEmpty || choices != 1) return Invalid<ExerciseMediaMutationResult>();
        var hash = MutationHash("change", exerciseId.ToString(), input.ExpectedImage, input.UploadReference?.ToString(), input.Remove, input.RestoreImage);
        var previous = await sessions.FindOperationAsync(ActorId(actor!), input.OperationId, cancellationToken);
        if (previous is not null && (previous.Status == "bound" || previous.OperationHash != hash)) return Replay(previous, hash);
        var exercise = await exercises.FindGlobalByIdAsync(exerciseId, cancellationToken);
        if (exercise is not { IsDeleted: false }) return Result<ExerciseMediaMutationResult, AppError>.Failure(new NotFoundError(Messages.DidntFind));
        if (!string.Equals(exercise.Image, input.ExpectedImage, StringComparison.Ordinal)) return Conflict<ExerciseMediaMutationResult>();
        var session = previous is not null ? await sessions.FindAsync(previous.Id, cancellationToken) : input.UploadReference.HasValue ? await sessions.FindAsync(input.UploadReference.Value, cancellationToken) : null;
        if (previous is null && input.UploadReference.HasValue && !Usable(session, actor!, exerciseId)) return IsExpired(session, actor!, exerciseId) ? Expired<ExerciseMediaMutationResult>() : Invalid<ExerciseMediaMutationResult>();
        if (session is not null && session.ExpiresAt <= DateTimeOffset.UtcNow) return session.Status == "publishing" ? Retry<ExerciseMediaMutationResult>() : Expired<ExerciseMediaMutationResult>();
        if (input.RestoreImage is not null && !await sessions.WasRetainedAsync(exerciseId, input.RestoreImage, cancellationToken)) return Invalid<ExerciseMediaMutationResult>();
        try
        {
            if (previous is null)
            {
                session ??= new ExerciseMediaSession { Id = Id<ExerciseMediaUpload>.New(), ActorId = ActorId(actor!) };
                if (!input.UploadReference.HasValue) await sessions.AddAsync(session, cancellationToken);
                Reserve(session, input.OperationId, hash, exerciseId, exercise.Image);
                await sessions.SaveSessionRevisionAsync(session, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            if (session is null || session.Status != "publishing") return Conflict<ExerciseMediaMutationResult>();
            var image = input.UploadReference.HasValue ? await PublishAsync(session, exerciseId, cancellationToken) : input.RestoreImage;
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            Bind(session, image);
            await sessions.SaveSessionRevisionAsync(session, cancellationToken);
            await sessions.StageImageAsync(exerciseId, input.ExpectedImage, image, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<ExerciseMediaMutationResult, AppError>.Success(new(exerciseId.ToString(), image));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (sessions.IsConcurrencyFailure(ex)) { return Conflict<ExerciseMediaMutationResult>(); }
        catch (Exception exception) { return Retry<ExerciseMediaMutationResult>(exception, "change", input.OperationId.ToString()); }
    }

    private static bool Usable(ExerciseMediaSession? session, AuthenticatedAccountContext actor, Id<Exercise>? exerciseId) => session is { Status: "verified" } && session.ActorId == ActorId(actor) && session.TargetExerciseId == exerciseId;
    private static bool IsExpired(ExerciseMediaSession? session, AuthenticatedAccountContext actor, Id<Exercise>? exerciseId) => session is { Status: "expired" } && session.ActorId == ActorId(actor) && session.TargetExerciseId == exerciseId;
    private static void Reserve(ExerciseMediaSession session, Id<ExerciseMediaOperationReference> operationId, string hash, Id<Exercise> id, string? previous)
    {
        // Durable publication intent precedes R2; retries reuse this target and digest-derived key.
        session.Status = "publishing";
        session.OperationId = operationId;
        session.OperationHash = hash;
        session.TargetExerciseId = id;
        session.PreviousImage = previous;
        session.ExpiresAt = DateTimeOffset.UtcNow.AddHours(24);
    }
    private static void Bind(ExerciseMediaSession session, string? image)
    {
        session.Status = "bound";
        session.ResultImage = image;
    }

    private static Result<ExerciseMediaMutationResult, AppError> Replay(ExerciseMediaSession session, string hash) => session.OperationHash == hash && session.Status == "bound" && session.TargetExerciseId.HasValue
        ? Result<ExerciseMediaMutationResult, AppError>.Success(new(session.TargetExerciseId.Value.ToString(), session.ResultImage)) : Conflict<ExerciseMediaMutationResult>();

    private async Task<string> PublishAsync(ExerciseMediaSession session, Id<Exercise> id, CancellationToken cancellationToken)
    {
        var bytes = await storage.ReadPrivateAsync(ExerciseMediaLimits.VerifiedKey(session), cancellationToken);
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != session.PublishedSha256) throw new ExerciseMediaRejectedException();
        var filename = session.Extension == "gif" ? "animation.gif" : $"image.{session.Extension}";
        return await storage.PublishAsync($"exercises/{id}/{session.PublishedSha256}/{filename}", bytes, session.ContentType, cancellationToken);
    }

    public async Task<Result<ExerciseMediaCleanupResult, AppError>> CleanupAsync(AuthenticatedAccountContext? actor, CancellationToken cancellationToken)
    {
        if (!Allowed(actor)) return Forbidden<ExerciseMediaCleanupResult>();
        var candidates = await sessions.CleanupCandidatesAsync(DateTimeOffset.UtcNow, cancellationToken);
        var cleaned = 0;
        foreach (var session in candidates)
        {
            try
            {
                await storage.DeletePrivateAsync(ExerciseMediaLimits.StagingKey(session.Id), cancellationToken);
                if (session.PublishedSha256 is not null) await storage.DeletePrivateAsync(ExerciseMediaLimits.VerifiedKey(session), cancellationToken);
                session.PrivateObjectsCleaned = true;
                if (session.Status is "pending" or "verified" or "publishing") session.Status = "expired";
                await sessions.SaveSessionRevisionAsync(session, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                cleaned++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                logger.LogWarning("Exercise media cleanup deferred: session={Session}, category={Category}", session.Id, exception.GetType().Name);
            }
        }
        return Result<ExerciseMediaCleanupResult, AppError>.Success(new(cleaned, candidates.Count - cleaned));
    }
}
