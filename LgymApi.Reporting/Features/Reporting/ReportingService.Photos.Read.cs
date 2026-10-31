using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using LgymApi.Identity.Contracts.Accounts;

namespace LgymApi.Application.Features.Reporting;

public sealed partial class ReportingService
{
    public async Task<Result<SignedReadUrlResult, AppError>> GetSignedReadUrlAsync(
        AuthenticatedAccountContext currentUser,
        Id<Photo> photoId,
        CancellationToken cancellationToken = default)
    {
        if (photoId.IsEmpty)
        {
            return Result<SignedReadUrlResult, AppError>.Failure(
                new InvalidReportingError(Messages.FieldRequired));
        }

        var photo = await _photoPersistence.FindByIdAsync(photoId, cancellationToken);
        if (photo == null || photo.IsDeleted)
        {
            return Result<SignedReadUrlResult, AppError>.Failure(
                new ReportingNotFoundError(Messages.DidntFind));
        }

        var authCheck = await ValidatePhotoAccessAsync(currentUser, photo.OwnerAccountId, cancellationToken);
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
        AuthenticatedAccountContext currentUser,
        GetPhotoHistoryCommand command,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ReportPhotoPersistenceModel> photos;
        var requests = new Dictionary<Id<ReportRequest>, ReportRequestPersistenceModel>();

        if (command.RequestId.HasValue && !command.RequestId.Value.IsEmpty)
        {
            var reportRequest = await _requestSubmissionPersistence.FindRequestByIdAsync(command.RequestId.Value, cancellationToken);
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

            photos = await _photoPersistence.ListByRequestAsync(command.RequestId.Value, cancellationToken);
            requests[reportRequest.Id] = reportRequest;
        }
        else if (command.TraineeId.HasValue && !command.TraineeId.Value.IsEmpty)
        {
            var authCheck = await ValidatePhotoAccessAsync(currentUser, command.TraineeId.Value, cancellationToken);
            if (authCheck.IsFailure)
            {
                return Result<List<PhotoHistoryItemResult>, AppError>.Failure(authCheck.Error);
            }

            photos = await _photoPersistence.ListByTraineeAsync(command.TraineeId.Value, cancellationToken);
        }
        else
        {
            return Result<List<PhotoHistoryItemResult>, AppError>.Failure(
                new InvalidReportingError("Either traineeId or requestId must be provided"));
        }

        var results = new List<PhotoHistoryItemResult>();
        foreach (var photo in photos)
        {
            if (!requests.TryGetValue(photo.ReportRequestId, out var request))
            {
                request = await _requestSubmissionPersistence.FindRequestByIdAsync(photo.ReportRequestId, cancellationToken);
                if (request is not null)
                {
                    requests[photo.ReportRequestId] = request;
                }
            }

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

            ResolvedPhotoBinding? binding = null;
            if (request is not null && photo.SourcePhotoTypeId.HasValue && !string.IsNullOrWhiteSpace(photo.ReportPhotoSlotId))
            {
                var bindingResult = ResolvePhotoBinding(request, photo.SourcePhotoTypeId, photo.ReportPhotoSlotId, photo.ViewType);
                if (bindingResult.IsSuccess)
                {
                    binding = bindingResult.Value;
                }
            }

            results.Add(new PhotoHistoryItemResult
            {
                Id = photo.Id,
                ViewType = photo.ViewType,
                SizeBytes = photo.SizeBytes,
                ThumbnailUrl = thumbnailUrl,
                ReadUrl = readUrl,
                ReportRequestId = photo.ReportRequestId,
                UploadedAt = photo.CreatedAt,
                PhotoTypeId = photo.SourcePhotoTypeId?.ToString(),
                ReportPhotoSlotId = photo.ReportPhotoSlotId,
                DisplayName = binding?.DisplayName,
                Labels = binding?.Labels,
                DefinitionSnapshotProvenance = request?.DefinitionSnapshot?.Provenance
            });
        }

        return Result<List<PhotoHistoryItemResult>, AppError>.Success(results);
    }
}
