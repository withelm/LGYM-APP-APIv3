using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.WorkoutProgress.Errors;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Authorization;
using LgymApi.Resources;
using Microsoft.Extensions.Logging;

namespace LgymApi.Application.WorkoutProgress.ExerciseMedia;

internal sealed class ExerciseMediaService(
    IExerciseMediaStorage storage,
    IExerciseMediaProcessingScheduler scheduler,
    IAuthenticatedAccountAuthorizationService authorization,
    ILogger<ExerciseMediaService> logger) : IExerciseMediaService
{
    public async Task<Result<ExerciseMediaUploadResult, AppError>> UploadAsync(AuthenticatedAccountContext? actor, byte[] bytes, CancellationToken cancellationToken)
    {
        if (actor is null || !authorization.HasPermission(actor, AuthConstants.Permissions.ManageGlobalExercises))
            return Result<ExerciseMediaUploadResult, AppError>.Failure(new ForbiddenError(Messages.Forbidden));
        if (!storage.IsConfigured)
            return Result<ExerciseMediaUploadResult, AppError>.Failure(new ExerciseMediaUnavailableError());
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (bytes.Length is <= 0 or > ExerciseMediaLimits.MaxBytes) throw new ExerciseMediaRejectedException();
            var image = await scheduler.ProcessAsync(Id<CorrelationScope>.New().ToString(), bytes, cancellationToken);
            return Result<ExerciseMediaUploadResult, AppError>.Success(new(image));
        }
        catch (ExerciseMediaRejectedException)
        {
            return Result<ExerciseMediaUploadResult, AppError>.Failure(new InvalidExerciseMediaError());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            // Provider messages/URLs may include credentials; log only the failure category.
            logger.LogWarning("Exercise media upload failed ({FailureType})", exception.GetType().Name);
            return Result<ExerciseMediaUploadResult, AppError>.Failure(new ExerciseMediaUnavailableError());
        }
    }
}
