using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts.Accounts;

namespace LgymApi.Application.WorkoutProgress.ExerciseMedia;

public static class ExerciseMediaLimits
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxDimension = 2048;
    public const int MaxFrames = 100;
    public const long MaxDecodedPixels = 16_000_000;
    public static readonly string[] ContentTypes = ["image/gif", "image/png", "image/jpeg", "image/webp"];
    public static string StagingKey(Id<ExerciseMediaUpload> id) => $"staging/{id}/source";
    public static string VerifiedKey(ExerciseMediaSession session) => $"verified/{session.Id}/{session.PublishedSha256}";
}

public sealed record InitiateExerciseMediaInput(string ContentType, long SizeBytes, string Sha256, Id<Exercise>? ExerciseId);
public sealed record CreateExerciseMediaInput(Id<ExerciseMediaOperationReference> OperationId, string Name, BodyParts BodyPart, ExerciseEloFormula? EloFormula, string? Description, Id<ExerciseMediaUpload>? UploadReference);
public sealed record ChangeExerciseMediaInput(Id<ExerciseMediaOperationReference> OperationId, string? ExpectedImage, Id<ExerciseMediaUpload>? UploadReference, bool Remove, string? RestoreImage);
public sealed record ExerciseMediaUploadResult(string UploadReference, string UploadUrl, DateTimeOffset ExpiresAt, Dictionary<string, string> Headers);
public sealed record ExerciseMediaVerificationResult(string UploadReference, string Status, string SourceSha256, string PublishedSha256, string ContentType, int Width, int Height, int Frames);
public sealed record ExerciseMediaMutationResult(string Id, string? Image, string Status = "persisted");
public sealed record ExerciseMediaCleanupResult(int Cleaned, int Remaining);
public sealed record VerifiedExerciseMedia(byte[] Bytes, string ContentType, string Extension, string SourceSha256, string PublishedSha256, int Width, int Height, int Frames);

public interface IExerciseMediaService
{
    Task<Result<ExerciseMediaUploadResult, AppError>> InitiateAsync(AuthenticatedAccountContext? actor, InitiateExerciseMediaInput input, CancellationToken cancellationToken);
    Task<Result<ExerciseMediaVerificationResult, AppError>> VerifyAsync(AuthenticatedAccountContext? actor, Id<ExerciseMediaUpload> reference, CancellationToken cancellationToken);
    Task<Result<Unit, AppError>> CancelAsync(AuthenticatedAccountContext? actor, Id<ExerciseMediaUpload> reference, CancellationToken cancellationToken);
    Task<Result<ExerciseMediaMutationResult, AppError>> CreateAsync(AuthenticatedAccountContext? actor, CreateExerciseMediaInput input, CancellationToken cancellationToken);
    Task<Result<ExerciseMediaMutationResult, AppError>> ChangeAsync(AuthenticatedAccountContext? actor, Id<Exercise> exerciseId, ChangeExerciseMediaInput input, CancellationToken cancellationToken);
    Task<Result<ExerciseMediaCleanupResult, AppError>> CleanupAsync(AuthenticatedAccountContext? actor, CancellationToken cancellationToken);
}

public interface IExerciseMediaStorage
{
    bool IsConfigured { get; }
    Task<string> SignUploadAsync(string key, string contentType, DateTimeOffset expiresAt, CancellationToken cancellationToken);
    Task<byte[]> ReadPrivateAsync(string key, CancellationToken cancellationToken);
    Task WriteVerifiedAsync(string key, byte[] bytes, string contentType, CancellationToken cancellationToken);
    Task<string> PublishAsync(string key, byte[] bytes, string contentType, CancellationToken cancellationToken);
    Task DeletePrivateAsync(string key, CancellationToken cancellationToken);
}

public interface IExerciseMediaVerifier
{
    Task<VerifiedExerciseMedia> VerifyAsync(byte[] bytes, string contentType, string sourceSha256, CancellationToken cancellationToken);
}

public sealed class ExerciseMediaRejectedException : Exception;


/// <summary>Stage-only persistence. Application owns transactions and commits.</summary>
public interface IExerciseMediaPersistence
{
    Task<ExerciseMediaSession?> FindAsync(Id<ExerciseMediaUpload> id, CancellationToken cancellationToken);
    Task<ExerciseMediaSession?> FindOperationAsync(Id<LgymApi.Identity.Contracts.AccountReference> actorId, Id<ExerciseMediaOperationReference> operationId, CancellationToken cancellationToken);
    Task<int> CountRecentAsync(Id<LgymApi.Identity.Contracts.AccountReference> actorId, DateTimeOffset since, CancellationToken cancellationToken);
    Task AddAsync(ExerciseMediaSession session, CancellationToken cancellationToken);
    Task StageImageAsync(Id<Exercise> exerciseId, string? expectedImage, string? newImage, CancellationToken cancellationToken);
    Task<bool> WasRetainedAsync(Id<Exercise> exerciseId, string image, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExerciseMediaSession>> CleanupCandidatesAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task SaveSessionRevisionAsync(ExerciseMediaSession session, CancellationToken cancellationToken);
    bool IsConcurrencyFailure(Exception exception);
}
