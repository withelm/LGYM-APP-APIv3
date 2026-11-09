using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;

namespace LgymApi.Application.WorkoutProgress.ExerciseMedia;

public static class ExerciseMediaLimits
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxRequestBytes = MaxBytes + 64 * 1024;
    public const int MaxDimension = 2048;
    public const int MaxFrames = 100;
    public const long MaxDecodedPixels = 16_000_000;
    public static readonly string[] ContentTypes = ["image/gif", "image/png", "image/jpeg", "image/webp"];
}

public sealed record ExerciseMediaSourceInput(string ContentType, long SizeBytes, string SourceSha256);
public sealed record CreateExerciseMediaInput(Id<ExerciseMediaOperationReference> OperationId, string Name, BodyParts BodyPart, ExerciseEloFormula? EloFormula, string? Description, ExerciseMediaSourceInput? Source);
public sealed record ChangeExerciseMediaInput(Id<ExerciseMediaOperationReference> OperationId, string? ExpectedImage, ExerciseMediaSourceInput? Source, bool Remove, string? RestoreImage);
public sealed record ExerciseMediaMutationResult(string Id, string? Image, string Status = "persisted");
public sealed record ExerciseMediaFailureResult(string OperationId, string Message, string Disposition);
public sealed record ExerciseMediaCleanupResult(int Cleaned, int Remaining);
public sealed record VerifiedExerciseMedia(byte[] Bytes, string ContentType, string Extension, string SourceSha256, string PublishedSha256, int Width, int Height, int Frames);

public interface IExerciseMediaService
{
    Task<Result<ExerciseMediaMutationResult, AppError>> CreateAsync(AuthenticatedAccountContext? actor, CreateExerciseMediaInput input, byte[]? sourceBytes, CancellationToken cancellationToken);
    Task<Result<ExerciseMediaMutationResult, AppError>> ChangeAsync(AuthenticatedAccountContext? actor, Id<Exercise> exerciseId, ChangeExerciseMediaInput input, byte[]? sourceBytes, CancellationToken cancellationToken);
    Task<bool> RejectAsync(AuthenticatedAccountContext? actor, Id<ExerciseMediaOperationReference> operationId, int statusCode, CancellationToken cancellationToken);
    Task<Result<ExerciseMediaCleanupResult, AppError>> CleanupAsync(AuthenticatedAccountContext? actor, CancellationToken cancellationToken);
}

public interface IExerciseMediaStorage
{
    bool IsConfigured { get; }
    string GetPublicUrl(string key);
    Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken);
    Task<string> PublishAsync(string key, byte[] bytes, string contentType, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

public interface IExerciseMediaVerifier
{
    Task<VerifiedExerciseMedia> VerifyAsync(byte[] bytes, string contentType, string sourceSha256, CancellationToken cancellationToken);
}

public sealed class ExerciseMediaRejectedException : Exception;

/// <summary>Stage-only operation and immutable-key fences. Application owns commits and transactions.</summary>
public interface IExerciseMediaPersistence
{
    Task<ExerciseMediaSession?> FindOperationAsync(Id<AccountReference> actorId, Id<ExerciseMediaOperationReference> operationId, CancellationToken cancellationToken);
    Task AddAsync(ExerciseMediaSession session, CancellationToken cancellationToken);
    Task SaveSessionRevisionAsync(ExerciseMediaSession session, CancellationToken cancellationToken);
    Task StageImageAsync(Id<Exercise> exerciseId, string? expectedImage, string? newImage, CancellationToken cancellationToken);
    Task<bool> WasRetainedAsync(Id<Exercise> exerciseId, string image, CancellationToken cancellationToken);
    Task<ExerciseMediaObjectState?> FindObjectAsync(string objectKey, CancellationToken cancellationToken);
    Task AddObjectAsync(ExerciseMediaObjectState state, CancellationToken cancellationToken);
    Task SaveObjectRevisionAsync(ExerciseMediaObjectState state, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExerciseMediaObjectState>> CleanupCandidatesAsync(DateTimeOffset before, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExerciseMediaSession>> SessionsForObjectAsync(string objectKey, CancellationToken cancellationToken);
    Task<bool> IsImageReferencedAsync(string image, CancellationToken cancellationToken);
    bool IsConcurrencyFailure(Exception exception);
}
