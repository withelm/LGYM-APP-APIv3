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
            return Result<InitiatePhotoUploadResult, AppError>.Failure(authCheck.Error);
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

    public async Task<Result<SignedReadUrlResult, AppError>> GetSignedReadUrlAsync(
        UserEntity currentUser,
        string photoId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(photoId))
        {
            return Result<SignedReadUrlResult, AppError>.Failure(
                new InvalidReportingError(Messages.FieldRequired));
        }

        if (!Id<Domain.Entities.Photo>.TryParse(photoId, out var parsedPhotoId))
        {
            return Result<SignedReadUrlResult, AppError>.Failure(
                new InvalidReportingError("Invalid photo ID format"));
        }

        var photo = await _reportingRepository.FindPhotoByIdAsync(parsedPhotoId, cancellationToken);
        if (photo == null || photo.IsDeleted)
        {
            return Result<SignedReadUrlResult, AppError>.Failure(
                new ReportingNotFoundError(Messages.DidntFind));
        }

        var authCheck = await ValidatePhotoAccessAsync(currentUser, photo.OwnerUserId, cancellationToken);
        if (authCheck.IsFailure)
        {
            return Result<SignedReadUrlResult, AppError>.Failure(authCheck.Error);
        }

        var readUrl = await _photoStorageProvider.GenerateSignedReadUrlAsync(
            photo.StorageKey,
            GetSignedReadExpiration(),
            cancellationToken);

        return Result<SignedReadUrlResult, AppError>.Success(new SignedReadUrlResult
        {
            ReadUrl = readUrl,
            ExpiresAt = DateTimeOffset.UtcNow.Add(GetSignedReadExpiration())
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

    public async Task<Result<List<PhotoHistoryItemResult>, AppError>> GetPhotoHistoryAsync(
        UserEntity currentUser,
        GetPhotoHistoryCommand command,
        CancellationToken cancellationToken = default)
    {
        List<Domain.Entities.Photo> photos;

        if (command.RequestId.HasValue && !command.RequestId.Value.IsEmpty)
        {
            var reportRequest = await _reportingRepository.FindRequestByIdAsync(command.RequestId.Value, cancellationToken);
            if (reportRequest == null)
            {
                return Result<List<PhotoHistoryItemResult>, AppError>.Failure(
                    new InvalidReportingError("Report request not found"));
            }

            var authCheck = await ValidatePhotoAccessAsync(currentUser, reportRequest.TraineeId, cancellationToken);
            if (authCheck.IsFailure)
            {
                return Result<List<PhotoHistoryItemResult>, AppError>.Failure(authCheck.Error);
            }

            photos = await _reportingRepository.GetPhotosByRequestIdAsync(command.RequestId.Value, cancellationToken);
        }
        else if (command.TraineeId.HasValue && !command.TraineeId.Value.IsEmpty)
        {
            var authCheck = await ValidatePhotoAccessAsync(currentUser, command.TraineeId.Value, cancellationToken);
            if (authCheck.IsFailure)
            {
                return Result<List<PhotoHistoryItemResult>, AppError>.Failure(authCheck.Error);
            }

            photos = await _reportingRepository.GetPhotosByTraineeIdAsync(command.TraineeId.Value, cancellationToken);
        }
        else
        {
            return Result<List<PhotoHistoryItemResult>, AppError>.Failure(
                new InvalidReportingError("Either traineeId or requestId must be provided"));
        }

        var results = new List<PhotoHistoryItemResult>();
        foreach (var photo in photos)
        {
            var readUrl = await _photoStorageProvider.GenerateSignedReadUrlAsync(
                photo.StorageKey,
                GetSignedReadExpiration(),
                cancellationToken);

            string? thumbnailUrl = null;
            if (!string.IsNullOrWhiteSpace(photo.ThumbnailStorageKey))
            {
                thumbnailUrl = await _photoStorageProvider.GenerateSignedReadUrlAsync(
                    photo.ThumbnailStorageKey,
                    GetSignedReadExpiration(),
                    cancellationToken);
            }

            results.Add(new PhotoHistoryItemResult
            {
                Id = photo.Id,
                ViewType = photo.ViewType.ToString(),
                SizeBytes = photo.SizeBytes,
                ThumbnailUrl = thumbnailUrl,
                ReadUrl = readUrl,
                ReportRequestId = photo.ReportRequestId,
                UploadedAt = photo.CreatedAt
            });
        }

        return Result<List<PhotoHistoryItemResult>, AppError>.Success(results);
    }

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
        var uuid = Guid.NewGuid().ToString("N");

        return $"photos/{traineeId}/{reportRequestId}/{viewType}/{timestamp}-{uuid}.{fileExtension}";
    }

    private string BuildStorageKeyPrefix(
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

        if (!string.IsNullOrWhiteSpace(command.Checksum) &&
            !string.Equals(NormalizeChecksum(command.Checksum), NormalizeChecksum(metadata.ETag), StringComparison.OrdinalIgnoreCase))
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

    private static string NormalizeChecksum(string checksum)
    {
        return checksum.Trim().Trim('"');
    }

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
