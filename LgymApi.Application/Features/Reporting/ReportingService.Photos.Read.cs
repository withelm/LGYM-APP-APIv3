using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.Reporting;

public sealed partial class ReportingService
{
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

        if (!Id<Photo>.TryParse(photoId, out var parsedPhotoId))
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

    public async Task<Result<List<PhotoHistoryItemResult>, AppError>> GetPhotoHistoryAsync(
        UserEntity currentUser,
        GetPhotoHistoryCommand command,
        CancellationToken cancellationToken = default)
    {
        List<Photo> photos;

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
}
