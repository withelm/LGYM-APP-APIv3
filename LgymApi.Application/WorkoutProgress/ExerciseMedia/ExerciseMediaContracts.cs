using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Identity.Contracts.Accounts;

namespace LgymApi.Application.WorkoutProgress.ExerciseMedia;

public static class ExerciseMediaLimits
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxRequestBytes = MaxBytes + 64 * 1024;
    public const int MaxDimension = 2048;
    public const int MaxFrames = 100;
    public const long MaxDecodedPixels = 16_000_000;
}

public sealed record ExerciseMediaUploadResult(string Image);
public sealed record VerifiedExerciseMedia(byte[] Bytes, string ContentType, string Extension, string PublishedSha256);

public interface IExerciseMediaService
{
    Task<Result<ExerciseMediaUploadResult, AppError>> UploadAsync(AuthenticatedAccountContext? actor, byte[] bytes, CancellationToken cancellationToken);
}

public interface IExerciseMediaStorage
{
    bool IsConfigured { get; }
    bool OwnsPublicImage(string image);
    Task DeleteAsync(string image, CancellationToken cancellationToken);
    Task<string> PublishAsync(string key, byte[] bytes, string contentType, CancellationToken cancellationToken);
}

public interface IExerciseMediaVerifier
{
    Task<VerifiedExerciseMedia> VerifyAsync(byte[] bytes, CancellationToken cancellationToken);
}

public sealed class ExerciseMediaRejectedException : Exception;
