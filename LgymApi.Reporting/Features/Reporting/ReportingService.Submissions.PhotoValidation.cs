using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;

namespace LgymApi.Application.Features.Reporting;

public sealed partial class ReportingService
{
    private async Task<Result<Unit, AppError>> ValidateRequiredPhotosAsync(
        ReportRequestPersistenceModel request,
        CancellationToken cancellationToken)
    {
        var requiredPhotoModuleConfigs = GetRequiredPhotoModuleConfigs(request);
        if (requiredPhotoModuleConfigs.Count == 0)
        {
            return Result<Unit, AppError>.Success(Unit.Value);
        }

        var allRequiredViews = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allRequiredPhotoTypeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allRequiredBindings = new HashSet<(string SlotId, string PhotoTypeId)>();
        var legacyBindings = new Dictionary<(string SlotId, string PhotoTypeId), string>();
        foreach (var moduleConfig in requiredPhotoModuleConfigs)
        {
            if (string.IsNullOrWhiteSpace(moduleConfig))
            {
                return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
            }

            try
            {
                var config = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(moduleConfig);
                var snapshotBindings = ReadSnapshotPhotoBindings(config).ToList();
                foreach (var binding in snapshotBindings)
                {
                    allRequiredBindings.Add((binding.ReportPhotoSlotId!, binding.PhotoTypeId!.Value.ToString()));
                    if (binding.LegacyCompatible && config.TryGetProperty("legacyViewTypes", out var legacyViews)
                        && legacyViews.ValueKind == System.Text.Json.JsonValueKind.Array)
                        legacyBindings[(binding.ReportPhotoSlotId!, binding.PhotoTypeId.Value.ToString())] = binding.ViewType;
                }

                if (config.TryGetProperty("requiredPhotoTypeIds", out var photoTypeIds))
                {
                    if (photoTypeIds.ValueKind != System.Text.Json.JsonValueKind.Array)
                    {
                        return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
                    }

                    foreach (var photoTypeId in photoTypeIds.EnumerateArray())
                    {
                        if (photoTypeId.ValueKind != System.Text.Json.JsonValueKind.String
                            || !Id<TrainerPhotoType>.TryParse(
                                photoTypeId.GetString() ?? string.Empty,
                                out var parsedPhotoTypeId))
                        {
                            return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
                        }

                        allRequiredPhotoTypeIds.Add(parsedPhotoTypeId.ToString());
                    }
                }
                else
                {
                    if (!ReportingModuleConfigParser.TryNormalizePhotoModuleConfig(config, out _, out var requiredViews))
                    {
                        return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
                    }

                    allRequiredViews.UnionWith(requiredViews);
                }
            }
            catch (System.Text.Json.JsonException)
            {
                return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
            }
        }

        if (allRequiredViews.Count == 0 && allRequiredPhotoTypeIds.Count == 0)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError(Messages.ReportFieldValidationFailed));
        }

        var uploadedPhotos = await _photoPersistence.ListByRequestAsync(request.Id, cancellationToken);
        var uploadedViews = uploadedPhotos
            .Where(p => !p.IsDeleted)
            .Select(p => p.ViewType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var uploadedPhotoTypeIds = uploadedPhotos
            .Where(photo => !photo.IsDeleted && photo.SourcePhotoTypeId.HasValue)
            .Select(photo => photo.SourcePhotoTypeId!.Value.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var uploadedBindings = uploadedPhotos
            .Where(photo => !photo.IsDeleted && photo.SourcePhotoTypeId.HasValue && !string.IsNullOrWhiteSpace(photo.ReportPhotoSlotId))
            .Select(photo => (photo.ReportPhotoSlotId!, photo.SourcePhotoTypeId!.Value.ToString()))
            .ToHashSet();
        foreach (var legacyBinding in legacyBindings)
        {
            if (uploadedPhotos.Any(photo => !photo.IsDeleted && photo.SourcePhotoTypeId is null
                && photo.ReportPhotoSlotId is null && string.Equals(photo.ViewType, legacyBinding.Value, StringComparison.OrdinalIgnoreCase)))
                uploadedBindings.Add(legacyBinding.Key);
        }

        var missingViews = allRequiredViews.Except(uploadedViews).ToList();
        var missingPhotoTypeIds = allRequiredBindings.Count == 0
            ? allRequiredPhotoTypeIds.Except(uploadedPhotoTypeIds).ToList()
            : [];
        var missingBindings = allRequiredBindings.Except(uploadedBindings).ToList();
        if (missingViews.Count > 0 || missingPhotoTypeIds.Count > 0 || missingBindings.Count > 0)
        {
            return Result<Unit, AppError>.Failure(new InvalidReportingError(
                Messages.ReportFieldValidationFailed,
                422,
                new { message = Messages.MissingRequiredPhotoViews }));
        }

        return Result<Unit, AppError>.Success(Unit.Value);
    }

    private static List<string?> GetRequiredPhotoModuleConfigs(ReportRequestPersistenceModel request)
    {
        if (request.DefinitionSnapshot is null)
        {
            return request.Template.Fields
                .Where(field => field.Type == ReportFieldType.Photos && field.IsRequired)
                .Select(field => field.ModuleConfig)
                .ToList();
        }

        var snapshot = ReportTemplateSnapshotFactory.Deserialize(request.DefinitionSnapshot.DefinitionJson);
        return snapshot.Fields
            .Where(field => field.Type == ReportFieldType.Photos && field.IsRequired)
            .Select(field => field.ModuleConfig.HasValue
                ? System.Text.Json.JsonSerializer.Serialize(field.ModuleConfig.Value)
                : null)
            .ToList();
    }
}
