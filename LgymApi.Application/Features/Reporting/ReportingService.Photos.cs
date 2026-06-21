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
    public async Task<Result<InitiatePhotoUploadResult, AppError>> InitiatePhotoUploadAsync(
        UserEntity currentUser,
        InitiatePhotoUploadCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.ReportRequestId.IsEmpty)
        {
            return Result<InitiatePhotoUploadResult, AppError>.Failure(
                new InvalidReportingError(Messages.FieldRequired));
        }

        var request = await _reportingRepository.FindRequestByIdAsync(command.ReportRequestId, cancellationToken);
        if (request == null || request.IsDeleted)
        {
            return Result<InitiatePhotoUploadResult, AppError>.Failure(
                new ReportingNotFoundError(Messages.DidntFind));
        }

        var authCheck = await ValidatePhotoAccessAsync(currentUser, request.TraineeId, cancellationToken);
        if (authCheck.IsFailure)
        {
            return authCheck.Error is ReportingForbiddenError
                ? Result<InitiatePhotoUploadResult, AppError>.Failure(new ReportingNotFoundError(Messages.DidntFind))
                : Result<InitiatePhotoUploadResult, AppError>.Failure(authCheck.Error);
        }

        var viewTypeValidation = TryParseViewType(command.ViewType, out var parsedViewType);
        if (viewTypeValidation.IsFailure)
        {
            return Result<InitiatePhotoUploadResult, AppError>.Failure(viewTypeValidation.Error);
        }

        var mimeTypeValidation = ValidateMimeType(command.MimeType);
        if (mimeTypeValidation.IsFailure)
        {
            return Result<InitiatePhotoUploadResult, AppError>.Failure(mimeTypeValidation.Error);
        }

        var sizeValidation = ValidateFileSize(command.SizeBytes);
        if (sizeValidation.IsFailure)
        {
            return Result<InitiatePhotoUploadResult, AppError>.Failure(sizeValidation.Error);
        }

        var limitValidation = await ValidateDeveloperLimitsAsync(currentUser.Id, cancellationToken);
        if (limitValidation.IsFailure)
        {
            _logger.LogWarning(
                "Photo upload-init rejected for user {UserId} and report request {ReportRequestId}: {Reason}",
                currentUser.Id,
                command.ReportRequestId,
                limitValidation.Error.Message);

            return Result<InitiatePhotoUploadResult, AppError>.Failure(limitValidation.Error);
        }

        var storageKey = GenerateStorageKey(
            request.TraineeId,
            command.ReportRequestId,
            parsedViewType,
            GetFileExtensionFromMimeType(command.MimeType));

        _logger.LogInformation(
            "Photo upload-init creating storage key with prefix {StorageKeyPrefix} for user {UserId}",
            BuildStorageKeyPrefix(request.TraineeId, command.ReportRequestId, parsedViewType),
            currentUser.Id);

        var uploadUrl = await _photoStorageProvider.GenerateSignedUploadUrlAsync(
            storageKey,
            command.MimeType,
            GetSignedUploadExpiration(),
            cancellationToken);

        var expiresAt = DateTimeOffset.UtcNow.Add(GetSignedUploadExpiration());

        await _photoUploadInitTracker.RecordUploadInitAsync(new PendingPhotoUpload
        {
            StorageKey = storageKey,
            InitiatedByUserId = currentUser.Id,
            OwnerUserId = request.TraineeId,
            ReportRequestId = command.ReportRequestId,
            ViewType = parsedViewType.ToString(),
            MimeType = command.MimeType,
            SizeBytes = command.SizeBytes,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = expiresAt
        }, cancellationToken);

        _logger.LogInformation(
            "Photo upload-init succeeded for user {UserId}, report request {ReportRequestId}, provider {Provider}, key {StorageKey}",
            currentUser.Id,
            command.ReportRequestId,
            _photoStorageOptions.Provider,
            storageKey);

        return Result<InitiatePhotoUploadResult, AppError>.Success(new InitiatePhotoUploadResult
        {
            UploadUrl = uploadUrl,
            StorageKey = storageKey,
            ExpiresAt = expiresAt
        });
    }

    public async Task<Result<CompletePhotoUploadResult, AppError>> CompletePhotoUploadAsync(
        UserEntity currentUser,
        CompletePhotoUploadCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.ReportRequestId.IsEmpty || string.IsNullOrWhiteSpace(command.StorageKey))
        {
            return Result<CompletePhotoUploadResult, AppError>.Failure(
                new InvalidReportingError(Messages.FieldRequired));
        }

        var request = await _reportingRepository.FindRequestByIdAsync(command.ReportRequestId, cancellationToken);
        if (request == null || request.IsDeleted)
        {
            return Result<CompletePhotoUploadResult, AppError>.Failure(
                new ReportingNotFoundError(Messages.DidntFind));
        }

        var authCheck = await ValidatePhotoAccessAsync(currentUser, request.TraineeId, cancellationToken);
        if (authCheck.IsFailure)
        {
            return Result<CompletePhotoUploadResult, AppError>.Failure(authCheck.Error);
        }

        var viewTypeValidation = TryParseViewType(command.ViewType, out var parsedViewType);
        if (viewTypeValidation.IsFailure)
        {
            return Result<CompletePhotoUploadResult, AppError>.Failure(viewTypeValidation.Error);
        }

        var mimeTypeValidation = ValidateMimeType(command.MimeType);
        if (mimeTypeValidation.IsFailure)
        {
            return Result<CompletePhotoUploadResult, AppError>.Failure(mimeTypeValidation.Error);
        }

        var sizeValidation = ValidateFileSize(command.SizeBytes);
        if (sizeValidation.IsFailure)
        {
            return Result<CompletePhotoUploadResult, AppError>.Failure(sizeValidation.Error);
        }

        var expectedPrefix = BuildStorageKeyPrefix(request.TraineeId, command.ReportRequestId, parsedViewType);
        if (!command.StorageKey.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Photo complete-upload rejected due to invalid storage key prefix for user {UserId}. Expected prefix {ExpectedPrefix}",
                currentUser.Id,
                expectedPrefix);

            return Result<CompletePhotoUploadResult, AppError>.Failure(
                new InvalidReportingError("Invalid storage key prefix"));
        }

        var pendingUpload = await _photoUploadInitTracker.GetPendingUploadAsync(command.StorageKey, cancellationToken);
        if (pendingUpload == null)
        {
            _logger.LogWarning(
                "Photo complete-upload rejected because no pending upload-init exists for key {StorageKey}",
                command.StorageKey);

            return Result<CompletePhotoUploadResult, AppError>.Failure(
                new InvalidReportingError("Upload session not found or expired"));
        }

        var pendingUploadValidation = ValidatePendingUpload(currentUser, command, request.TraineeId, parsedViewType, pendingUpload);
        if (pendingUploadValidation.IsFailure)
        {
            _logger.LogWarning(
                "Photo complete-upload rejected because pending upload data mismatched for key {StorageKey}: {Reason}",
                command.StorageKey,
                pendingUploadValidation.Error.Message);

            await _photoUploadInitTracker.RemovePendingUploadAsync(command.StorageKey, cancellationToken);

            return Result<CompletePhotoUploadResult, AppError>.Failure(pendingUploadValidation.Error);
        }

        PhotoMetadata? metadata;
        try
        {
            metadata = await _photoStorageProvider.GetMetadataAsync(command.StorageKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Photo complete-upload metadata lookup failed for key {StorageKey} using provider {Provider}",
                command.StorageKey,
                _photoStorageOptions.Provider);

            return Result<CompletePhotoUploadResult, AppError>.Failure(
                new InvalidReportingError("Failed to verify uploaded photo metadata"));
        }

        if (metadata == null)
        {
            _logger.LogWarning(
                "Photo complete-upload rejected because object metadata was not found for key {StorageKey}",
                command.StorageKey);

            return Result<CompletePhotoUploadResult, AppError>.Failure(
                new InvalidReportingError("Uploaded photo object was not found"));
        }

        var metadataValidation = ValidateUploadedObjectMetadata(command, metadata);
        if (metadataValidation.IsFailure)
        {
            _logger.LogWarning(
                "Photo complete-upload metadata verification failed for key {StorageKey}: {Reason}",
                command.StorageKey,
                metadataValidation.Error.Message);

            await CleanupInvalidUploadedObjectAsync(command.StorageKey, cancellationToken);
            await _photoUploadInitTracker.RemovePendingUploadAsync(command.StorageKey, cancellationToken);
            return Result<CompletePhotoUploadResult, AppError>.Failure(metadataValidation.Error);
        }

        var existingPhoto = await _reportingRepository.FindActivePhotoByRequestAndViewAsync(
            command.ReportRequestId,
            parsedViewType,
            cancellationToken);

        var photo = new Domain.Entities.Photo
        {
            Id = Id<Domain.Entities.Photo>.New(),
            StorageKey = command.StorageKey,
            MimeType = metadata.ContentType,
            SizeBytes = metadata.SizeBytes,
            Checksum = ResolveStoredChecksum(command.Checksum, metadata.ETag),
            ViewType = parsedViewType,
            ReportRequestId = command.ReportRequestId,
            UploaderUserId = currentUser.Id,
            OwnerUserId = request.TraineeId
        };

        await _reportingRepository.SavePhotoAsync(photo, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _photoUploadInitTracker.RemovePendingUploadAsync(command.StorageKey, cancellationToken);

        if (existingPhoto != null && !string.Equals(existingPhoto.StorageKey, photo.StorageKey, StringComparison.Ordinal))
        {
            try
            {
                await _photoStorageProvider.DeleteAsync(existingPhoto.StorageKey, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Photo complete-upload saved new metadata but failed to delete replaced object {StorageKey}",
                    existingPhoto.StorageKey);
            }
        }

        _logger.LogInformation(
            "Photo complete-upload succeeded for user {UserId}, request {ReportRequestId}, photo {PhotoId}",
            currentUser.Id,
            command.ReportRequestId,
            photo.Id);

        return Result<CompletePhotoUploadResult, AppError>.Success(new CompletePhotoUploadResult
        {
            PhotoId = photo.Id,
            UploadedAt = photo.CreatedAt
        });
    }

}
