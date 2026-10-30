using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Resources;
using Microsoft.Extensions.Logging;

namespace LgymApi.Application.Features.Reporting;

public sealed partial class ReportingService
{
    private async Task<Result<Unit, AppError>> ValidatePhotoAccessAsync(
        AuthenticatedAccountContext currentUser,
        Id<AccountReference> traineeId,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id == traineeId)
        {
            return Result<Unit, AppError>.Success(Unit.Value);
        }

        var trainerCheck = EnsureTrainer(currentUser);
        if (trainerCheck.IsFailure)
        {
            return Result<Unit, AppError>.Failure(new ReportingForbiddenError(Messages.TrainerRoleRequired));
        }

        var access = await _relationshipAccessPersistence.GetAccessAsync(currentUser.Id, traineeId, cancellationToken);
        if (!access.HasActiveRelationship)
        {
            return Result<Unit, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private static string GenerateStorageKey(
        Id<AccountReference> traineeId,
        Id<ReportRequest> reportRequestId,
        string viewType,
        string fileExtension)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH-mm-ssZ");
        var uniqueSuffix = Id<Photo>.New().ToString().Replace("-", string.Empty, StringComparison.Ordinal);
        var sanitizedViewType = SanitizeViewTypeSegment(viewType);

        return $"photos/{traineeId}/{reportRequestId}/{sanitizedViewType}/{timestamp}-{uniqueSuffix}.{fileExtension}";
    }

    private static string BuildStorageKeyPrefix(
        Id<AccountReference> traineeId,
        Id<ReportRequest> reportRequestId,
        string viewType)
    {
        return $"photos/{traineeId}/{reportRequestId}/{SanitizeViewTypeSegment(viewType)}/";
    }

    private Result<Unit, AppError> TryParseViewType(string viewType, out string parsedViewType)
    {
        if (!ReportingModuleConfigParser.TryNormalizePhotoViewName(viewType, out parsedViewType))
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError($"Invalid view type: {viewType}"));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private Result<Unit, AppError> ValidateMimeType(string mimeType)
    {
        if (_photoStorageOptions.AllowedMimeTypes.Any(x => string.Equals(x, mimeType, StringComparison.OrdinalIgnoreCase)))
        {
            return Result<Unit, AppError>.Success(Unit.Value);
        }

        return Result<Unit, AppError>.Failure(
            new InvalidReportingError($"Invalid MIME type: {mimeType}. Allowed types: {string.Join(", ", _photoStorageOptions.AllowedMimeTypes)}"));
    }

    private Result<Unit, AppError> ValidateFileSize(long sizeBytes)
    {
        if (sizeBytes <= 0)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError("File size must be greater than 0 bytes"));
        }

        if (sizeBytes <= _photoStorageOptions.MaxFileSizeBytes)
        {
            return Result<Unit, AppError>.Success(Unit.Value);
        }

        return Result<Unit, AppError>.Failure(
            new InvalidReportingError($"File size exceeds maximum: {sizeBytes} bytes (max: {_photoStorageOptions.MaxFileSizeBytes} bytes)"));
    }

    private async Task<Result<Unit, AppError>> ValidateDeveloperLimitsAsync(
        Id<AccountReference> currentUserId,
        CancellationToken cancellationToken)
    {
        var totalBytes = await _photoPersistence.GetActiveStorageBytesAsync(cancellationToken);
        if (totalBytes >= _photoStorageOptions.DevMaxTotalBytes)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError("Development photo storage byte limit reached"));
        }

        var startOfDayUtc = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var uploadsToday = await _photoPersistence.CountCreatedSinceAsync(startOfDayUtc, cancellationToken);
        if (uploadsToday >= _photoStorageOptions.DevMaxUploadsPerDay)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError("Development daily photo upload limit reached"));
        }

        var perUserWindowStart = DateTimeOffset.UtcNow.AddHours(-1);
        var recentUploadInits = await _photoPersistence.CountRecentUploadInitsAsync(currentUserId, perUserWindowStart, cancellationToken);
        if (recentUploadInits >= _photoStorageOptions.DevMaxUploadInitPerUserPerHour)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError("Development upload-init hourly limit reached"));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private bool ShouldEnforceDevelopmentPhotoLimits()
        => string.Equals(_photoStorageOptions.Provider, "Local", StringComparison.OrdinalIgnoreCase);

    private static string GetFileExtensionFromMimeType(string mimeType)
        => mimeType.ToLowerInvariant() switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/heic" => "heic",
            _ => "jpg"
        };

    private static Result<Unit, AppError> EnsureRequestAllowsPhotoUpload(ReportRequestPersistenceModel request)
        => request.Status is ReportRequestStatus.Pending or ReportRequestStatus.Expired
            ? Result<Unit, AppError>.Success(Unit.Value)
            : Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportRequestNotPending));
}
