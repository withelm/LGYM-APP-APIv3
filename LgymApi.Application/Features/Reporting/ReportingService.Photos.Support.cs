using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LgymApi.Application.Features.Reporting;

public sealed partial class ReportingService
{
    private sealed record ResolvedPhotoBinding(
        string ViewType,
        Id<TrainerPhotoType>? PhotoTypeId,
        string? ReportPhotoSlotId,
        string? DisplayName,
        PhotoTypeLabelsResult? Labels,
        bool LegacyCompatible = true);

    private Result<ResolvedPhotoBinding, AppError> ResolvePhotoBinding(
        ReportRequestPersistenceModel request,
        Id<TrainerPhotoType>? photoTypeId,
        string? reportPhotoSlotId,
        string requestedViewType)
    {
        if (request.DefinitionSnapshot is null)
        {
            if (photoTypeId.HasValue || !string.IsNullOrWhiteSpace(reportPhotoSlotId))
                return Result<ResolvedPhotoBinding, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
            var legacyView = TryParseViewType(requestedViewType, out var parsedLegacyView);
            return legacyView.IsFailure
                ? Result<ResolvedPhotoBinding, AppError>.Failure(legacyView.Error)
                : Result<ResolvedPhotoBinding, AppError>.Success(new ResolvedPhotoBinding(
                    parsedLegacyView, photoTypeId, reportPhotoSlotId, null, null));
        }

        var snapshot = JsonSerializer.Deserialize<ReportTemplateSnapshotDocument>(request.DefinitionSnapshot.DefinitionJson)
            ?? throw new InvalidOperationException("A report request snapshot is malformed.");
        var bindings = snapshot.Fields
            .Where(field => field.Type == ReportFieldType.Photos && field.ModuleConfig.HasValue)
            .SelectMany(field => ReadSnapshotPhotoBindings(field.ModuleConfig!.Value))
            .ToList();

        if (bindings.Count == 0)
        {
            if (photoTypeId.HasValue || !string.IsNullOrWhiteSpace(reportPhotoSlotId))
                return Result<ResolvedPhotoBinding, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
            var legacyView = TryParseViewType(requestedViewType, out var parsedLegacyView);
            return legacyView.IsFailure
                ? Result<ResolvedPhotoBinding, AppError>.Failure(legacyView.Error)
                : Result<ResolvedPhotoBinding, AppError>.Success(new ResolvedPhotoBinding(
                    parsedLegacyView, photoTypeId, reportPhotoSlotId, null, null));
        }

        if (!string.IsNullOrWhiteSpace(reportPhotoSlotId) && !Id<ReportPhotoSlot>.TryParse(reportPhotoSlotId, out _))
        {
            return Result<ResolvedPhotoBinding, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
        }

        var candidates = bindings.Where(candidate =>
            (!photoTypeId.HasValue || candidate.PhotoTypeId == photoTypeId)
            && (string.IsNullOrWhiteSpace(reportPhotoSlotId) || string.Equals(candidate.ReportPhotoSlotId, reportPhotoSlotId, StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrWhiteSpace(requestedViewType) || string.Equals(candidate.ViewType, requestedViewType.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (candidates.Count == 0 && !photoTypeId.HasValue && string.IsNullOrWhiteSpace(reportPhotoSlotId)
            && snapshot.Fields.Any(field => field.ModuleConfig.HasValue
                && field.ModuleConfig.Value.TryGetProperty("legacyViewTypes", out var legacy) && legacy.ValueKind == JsonValueKind.Array)
            && ReportingModuleConfigParser.TryNormalizePhotoViewName(requestedViewType, out var legacyExtraView))
            return Result<ResolvedPhotoBinding, AppError>.Success(new ResolvedPhotoBinding(legacyExtraView, null, null, null, null));

        if (candidates.Count != 1 || (!photoTypeId.HasValue &&
            (string.IsNullOrWhiteSpace(requestedViewType) || !candidates[0].LegacyCompatible)))
        {
            return Result<ResolvedPhotoBinding, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
        }

        return Result<ResolvedPhotoBinding, AppError>.Success(candidates[0]);
    }

    private static IEnumerable<ResolvedPhotoBinding> ReadSnapshotPhotoBindings(JsonElement moduleConfig)
    {
        if (!moduleConfig.TryGetProperty("photoTypes", out var photoTypes) || photoTypes.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var photoType in photoTypes.EnumerateArray())
        {
            if (!photoType.TryGetProperty("photoTypeId", out var photoTypeIdElement)
                || !Id<TrainerPhotoType>.TryParse(photoTypeIdElement.GetString() ?? string.Empty, out var photoTypeId)
                || !photoType.TryGetProperty("reportPhotoSlotId", out var slotElement)
                || string.IsNullOrWhiteSpace(slotElement.GetString()))
            {
                continue;
            }

            var systemKey = photoType.TryGetProperty("systemKey", out var systemKeyElement)
                && systemKeyElement.ValueKind == JsonValueKind.String
                    ? systemKeyElement.GetString()
                    : null;
            var viewType = !string.IsNullOrWhiteSpace(systemKey)
                && ReportingModuleConfigParser.TryNormalizePhotoViewName(systemKey, out var normalizedView)
                    ? normalizedView
                    : "Custom";
            PhotoTypeLabelsResult? labels = null;
            if (photoType.TryGetProperty("labels", out var labelsElement)
                && labelsElement.TryGetProperty("pl", out var pl)
                && labelsElement.TryGetProperty("en", out var en)
                && pl.ValueKind == JsonValueKind.String
                && en.ValueKind == JsonValueKind.String)
            {
                labels = new PhotoTypeLabelsResult(pl.GetString() ?? string.Empty, en.GetString() ?? string.Empty);
            }

            yield return new ResolvedPhotoBinding(
                viewType,
                photoTypeId,
                slotElement.GetString(),
                labels is null ? null : ReportingPhotoTypeLabels.DisplayName(labels),
                labels,
                systemKey is not null && photoType.TryGetProperty("nameMode", out var mode) &&
                ((mode.ValueKind == JsonValueKind.Number && mode.TryGetInt32(out var numericMode) && numericMode == (int)PhotoTypeNameMode.SystemLocalized)
                 || (mode.ValueKind == JsonValueKind.String && mode.GetString() == nameof(PhotoTypeNameMode.SystemLocalized))));
        }
    }
    private Result<Unit, AppError> ValidateUploadedObjectMetadata(CompletePhotoUploadCommand command, PhotoMetadata metadata)
        => ValidateUploadedObjectMetadata(command, null, metadata);

    private Result<Unit, AppError> ValidateUploadedObjectMetadata(
        CompletePhotoUploadCommand command,
        PendingPhotoUpload? uploadSession,
        PhotoMetadata metadata)
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

        var expectedMimeType = uploadSession?.DeclaredContentType ?? command.MimeType;
        if (!string.Equals(expectedMimeType, metadata.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError("Uploaded photo MIME type does not match the initiated upload"));
        }

        var expectedSizeBytes = uploadSession?.DeclaredSizeBytes ?? command.SizeBytes;
        if (metadata.SizeBytes != expectedSizeBytes)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError("Uploaded photo size does not match the initiated upload"));
        }

        if (command.SizeBytes != expectedSizeBytes)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError("Upload completion request does not match the initiated upload"));
        }

        if (!string.Equals(command.MimeType, expectedMimeType, StringComparison.OrdinalIgnoreCase))
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError("Upload completion request does not match the initiated upload"));
        }

        if (!string.IsNullOrWhiteSpace(command.Checksum)
            && !string.Equals(NormalizeChecksum(command.Checksum), NormalizeChecksum(metadata.ETag), StringComparison.OrdinalIgnoreCase))
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError("Uploaded photo checksum does not match object metadata"));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private Result<Unit, AppError> ValidatePendingUpload(
        AuthenticatedAccountContext currentUser,
        CompletePhotoUploadCommand command,
        Id<AccountReference> ownerUserId,
        ResolvedPhotoBinding binding,
        PendingPhotoUpload pendingUpload)
    {
        if (!string.Equals(pendingUpload.StorageKey, command.StorageKey, StringComparison.Ordinal)
            || pendingUpload.InitiatedByAccountId != currentUser.Id
            || pendingUpload.OwnerAccountId != ownerUserId
            || pendingUpload.ReportRequestId != command.ReportRequestId
            || !string.Equals(pendingUpload.ViewType, binding.ViewType, StringComparison.OrdinalIgnoreCase)
            || pendingUpload.SourcePhotoTypeId != binding.PhotoTypeId
            || !string.Equals(pendingUpload.ReportPhotoSlotId, binding.ReportPhotoSlotId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(pendingUpload.DeclaredContentType, command.MimeType, StringComparison.OrdinalIgnoreCase)
            || pendingUpload.DeclaredSizeBytes != command.SizeBytes)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError("Upload session does not match the original upload-init request"));
        }

        if (pendingUpload.Status == PhotoUploadSessionStatus.Completed)
            return Result<Unit, AppError>.Success(Unit.Value);

        if (pendingUpload.Status != PhotoUploadSessionStatus.Pending)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError("Upload session is not pending"));
        }

        if (pendingUpload.ExpiresAtUtc < DateTimeOffset.UtcNow)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError("Upload session not found or expired"));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private Result<Unit, AppError> EnsureStorageKeyHasExpectedPrefix(
        AuthenticatedAccountContext currentUser,
        string storageKey,
        Id<AccountReference> traineeId,
        Id<ReportRequest> reportRequestId,
        string parsedViewType)
    {
        var expectedPrefix = BuildStorageKeyPrefix(traineeId, reportRequestId, parsedViewType);
        if (storageKey.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            return Result<Unit, AppError>.Success(Unit.Value);
        }

        _logger.LogWarning(
            "Photo complete-upload rejected due to invalid storage key prefix for user {UserId}. Expected prefix {ExpectedPrefix}",
            currentUser.Id,
            expectedPrefix);

        return Result<Unit, AppError>.Failure(
            new InvalidReportingError("Invalid storage key prefix"));
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

    private static string SanitizeViewTypeSegment(string viewType)
    {
        var trimmed = viewType.Trim();
        if (trimmed.Length == 0)
        {
            return "Unknown";
        }

        var sanitizedChars = trimmed
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();

        var sanitized = new string(sanitizedChars);
        while (sanitized.Contains("--", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("--", "-", StringComparison.Ordinal);
        }

        return sanitized.Trim('-');
    }

    private TimeSpan GetSignedUploadExpiration() => TimeSpan.FromMinutes(_photoStorageOptions.SignedUploadExpirationMinutes);

    private TimeSpan GetSignedReadExpiration() => TimeSpan.FromMinutes(_photoStorageOptions.SignedReadExpirationMinutes);

}
