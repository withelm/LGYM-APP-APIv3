using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.Extensions.Logging;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Reporting;

public sealed partial class ReportingService
{
    private async Task<Result<Unit, AppError>> ValidatePhotoAccessAsync(
        UserEntity currentUser,
        Id<UserEntity> traineeId,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id == traineeId)
        {
            return Result<Unit, AppError>.Success(Unit.Value);
        }

        var isTrainer = await _roleRepository.UserHasRoleAsync(currentUser.Id, Domain.Security.AuthConstants.Roles.Trainer, cancellationToken);
        if (!isTrainer)
        {
            return Result<Unit, AppError>.Failure(new ReportingForbiddenError(Messages.TrainerRoleRequired));
        }

        var link = await _trainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(
            currentUser.Id,
            traineeId,
            cancellationToken);

        if (link == null)
        {
            return Result<Unit, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private static string GenerateStorageKey(
        Id<UserEntity> traineeId,
        Id<ReportRequest> reportRequestId,
        PhotoViewType viewType,
        string fileExtension)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH-mm-ssZ");
        var uniqueSuffix = Id<Photo>.New().ToString().Replace("-", string.Empty, StringComparison.Ordinal);

        return $"photos/{traineeId}/{reportRequestId}/{viewType}/{timestamp}-{uniqueSuffix}.{fileExtension}";
    }

    private static string BuildStorageKeyPrefix(
        Id<UserEntity> traineeId,
        Id<ReportRequest> reportRequestId,
        PhotoViewType viewType)
    {
        return $"photos/{traineeId}/{reportRequestId}/{viewType}/";
    }

    private Result<Unit, AppError> TryParseViewType(string viewType, out PhotoViewType parsedViewType)
    {
        if (!System.Enum.TryParse<PhotoViewType>(viewType, ignoreCase: true, out parsedViewType)
            || !System.Enum.IsDefined(parsedViewType))
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
        Id<UserEntity> currentUserId,
        CancellationToken cancellationToken)
    {
        var totalBytes = await _reportingRepository.GetActivePhotoStorageBytesAsync(cancellationToken);
        if (totalBytes >= _photoStorageOptions.DevMaxTotalBytes)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError("Development photo storage byte limit reached"));
        }

        var startOfDayUtc = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var uploadsToday = await _reportingRepository.CountPhotosCreatedSinceAsync(startOfDayUtc, cancellationToken);
        if (uploadsToday >= _photoStorageOptions.DevMaxUploadsPerDay)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError("Development daily photo upload limit reached"));
        }

        var perUserWindowStart = DateTimeOffset.UtcNow.AddHours(-1);
        var recentUploadInits = await _photoUploadInitTracker.CountRecentUploadInitsAsync(currentUserId, perUserWindowStart, cancellationToken);
        if (recentUploadInits >= _photoStorageOptions.DevMaxUploadInitPerUserPerHour)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError("Development upload-init hourly limit reached"));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private Result<Unit, AppError> ValidateUploadedObjectMetadata(CompletePhotoUploadCommand command, PhotoMetadata metadata)
    {
        var sizeValidation = ValidateFileSize(metadata.SizeBytes);
        if (sizeValidation.IsFailure)
        {
            return sizeValidation;
        }

        var mimeValidation = ValidateMimeType(metadata.ContentType);
        if (mimeValidation.IsFailure)
        {
            return mimeValidation;
        }

        if (!string.Equals(command.MimeType, metadata.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError("Uploaded photo MIME type does not match the initiated upload"));
        }

        if (command.SizeBytes != metadata.SizeBytes)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError("Uploaded photo size does not match the initiated upload"));
        }

        if (!string.IsNullOrWhiteSpace(command.Checksum)
            && !string.Equals(NormalizeChecksum(command.Checksum), NormalizeChecksum(metadata.ETag), StringComparison.OrdinalIgnoreCase))
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError("Uploaded photo checksum does not match object metadata"));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private Result<Unit, AppError> ValidatePendingUpload(
        UserEntity currentUser,
        CompletePhotoUploadCommand command,
        Id<UserEntity> ownerUserId,
        PhotoViewType parsedViewType,
        PendingPhotoUpload pendingUpload)
    {
        if (!string.Equals(pendingUpload.StorageKey, command.StorageKey, StringComparison.Ordinal)
            || pendingUpload.InitiatedByUserId != currentUser.Id
            || pendingUpload.OwnerUserId != ownerUserId
            || pendingUpload.ReportRequestId != command.ReportRequestId
            || !string.Equals(pendingUpload.ViewType, parsedViewType.ToString(), StringComparison.Ordinal)
            || !string.Equals(pendingUpload.MimeType, command.MimeType, StringComparison.OrdinalIgnoreCase)
            || pendingUpload.SizeBytes != command.SizeBytes)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError("Upload session does not match the original upload-init request"));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private async Task CleanupInvalidUploadedObjectAsync(string storageKey, CancellationToken cancellationToken)
    {
        try
        {
            await _photoStorageProvider.DeleteAsync(storageKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up invalid uploaded object {StorageKey}", storageKey);
        }
    }

    private string ResolveStoredChecksum(string clientChecksum, string metadataETag)
    {
        if (!string.IsNullOrWhiteSpace(metadataETag))
        {
            return NormalizeChecksum(metadataETag);
        }

        return NormalizeChecksum(clientChecksum);
    }

    private static string NormalizeChecksum(string checksum) => checksum.Trim().Trim('"');

    private TimeSpan GetSignedUploadExpiration() => TimeSpan.FromMinutes(_photoStorageOptions.SignedUploadExpirationMinutes);

    private TimeSpan GetSignedReadExpiration() => TimeSpan.FromMinutes(_photoStorageOptions.SignedReadExpirationMinutes);

    private static string GetFileExtensionFromMimeType(string mimeType)
    {
        return mimeType.ToLowerInvariant() switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/heic" => "heic",
            _ => "jpg"
        };
    }
}
