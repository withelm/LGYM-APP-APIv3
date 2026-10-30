using System.Globalization;
using System.Resources;
using System.Text.Json;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using LgymApi.Identity.Contracts;

namespace LgymApi.Application.Features.Reporting;

internal static class ReportTemplateSnapshotFactory
{
    public const string Provenance = "Issued";

    // Callers hold the trainer definition lock and commit these staged defaults with their operation.
    public static async Task<IReadOnlyList<TrainerPhotoTypePersistenceModel>> EnsureDefaultsAsync(
        IReportTemplatePersistence persistence, Id<AccountReference> trainerId, CancellationToken cancellationToken)
    {
        var types = (await persistence.ListPhotoTypesAsync(trainerId, cancellationToken)).ToList();
        foreach (var key in Enum.GetNames<PhotoViewType>())
        {
            if (types.Any(type => type.SystemKey == key)) continue;
            var item = new NewTrainerPhotoTypePersistenceModel(Id<TrainerPhotoType>.New(), trainerId, key, key,
                PhotoTypeNameMode.SystemLocalized, false, DateTimeOffset.UtcNow);
            await persistence.AddPhotoTypeAsync(item, cancellationToken);
            types.Add(new TrainerPhotoTypePersistenceModel(item.Id, trainerId, key, key, item.NameMode, false, item.CreatedAt));
        }
        return types;
    }

    public static Result<ReportTemplateSnapshotDocument, AppError> Create(
        ReportTemplatePersistenceModel template,
        IReadOnlyList<TrainerPhotoTypePersistenceModel> photoTypes)
    {
        var photoTypesById = photoTypes.ToDictionary(type => type.Id);
        var fields = new List<ReportTemplateSnapshotFieldDocument>(template.Fields.Count);

        foreach (var field in template.Fields)
        {
            var moduleConfig = string.IsNullOrWhiteSpace(field.ModuleConfig)
                ? (JsonElement?)null
                : JsonSerializer.Deserialize<JsonElement>(field.ModuleConfig);

            if (field.Type == ReportFieldType.Photos && moduleConfig.HasValue)
            {
                var selected = new List<TrainerPhotoTypePersistenceModel>();
                var legacy = moduleConfig.Value.TryGetProperty("requiredViews", out var requiredViews);
                if (legacy)
                {
                    foreach (var view in requiredViews.EnumerateArray())
                    {
                        var type = photoTypes.SingleOrDefault(item => string.Equals(item.SystemKey, view.GetString(), StringComparison.OrdinalIgnoreCase));
                        if (type is null || type.IsArchived)
                            return Result<ReportTemplateSnapshotDocument, AppError>.Failure(new ReportingConflictError(Messages.ReportFieldValidationFailed));
                        selected.Add(type);
                    }
                }
                else if (moduleConfig.Value.TryGetProperty("requiredPhotoTypeIds", out var requiredIds))
                {
                    foreach (var id in requiredIds.EnumerateArray())
                    {
                        if (!Id<TrainerPhotoType>.TryParse(id.GetString() ?? string.Empty, out var parsed)
                            || !photoTypesById.TryGetValue(parsed, out var type) || type.IsArchived)
                            return Result<ReportTemplateSnapshotDocument, AppError>.Failure(new ReportingConflictError(Messages.ReportFieldValidationFailed));
                        selected.Add(type);
                    }
                }
                var frozenPhotoTypes = new List<object>();
                foreach (var photoType in selected)
                {
                    var labels = ReportingPhotoTypeLabels.Resolve(photoType);
                    frozenPhotoTypes.Add(new
                    {
                        photoTypeId = photoType.Id.ToString(),
                        reportPhotoSlotId = Id<ReportPhotoSlot>.New().ToString(),
                        systemKey = photoType.SystemKey,
                        nameMode = photoType.NameMode,
                        labels = new { pl = labels.Pl, en = labels.En }
                    });
                }

                moduleConfig = JsonSerializer.SerializeToElement(new
                {
                    schemaVersion = 2,
                    legacyViewTypes = legacy ? selected.Select(type => type.SystemKey).ToArray() : null,
                    requiredPhotoTypeIds = selected.Select(type => type.Id.ToString()).ToArray(),
                    photoTypes = frozenPhotoTypes
                });
            }

            fields.Add(new ReportTemplateSnapshotFieldDocument(
                field.Key,
                field.Label,
                field.Type,
                field.IsRequired,
                field.Order,
                moduleConfig));
        }

        return Result<ReportTemplateSnapshotDocument, AppError>.Success(new ReportTemplateSnapshotDocument(
            template.Id.ToString(),
            template.Name,
            template.Description,
            fields,
            Provenance));
    }

    public static string Serialize(ReportTemplateSnapshotDocument snapshot)
        => JsonSerializer.Serialize(snapshot);

    public static ReportTemplatePersistenceModel EffectiveTemplate(ReportRequestPersistenceModel request)
    {
        if (request.DefinitionSnapshot is null)
        {
            return request.Template;
        }

        var snapshot = JsonSerializer.Deserialize<ReportTemplateSnapshotDocument>(request.DefinitionSnapshot.DefinitionJson)
            ?? throw new InvalidOperationException("A report request snapshot is malformed.");
        return new ReportTemplatePersistenceModel(
            request.TemplateId,
            request.TrainerId,
            snapshot.Name,
            snapshot.Description,
            request.DefinitionSnapshot.CreatedAt,
            false,
            snapshot.Fields.Select(field => new ReportTemplateFieldPersistenceModel(
                Id<ReportTemplateField>.New(),
                field.Key,
                field.Label,
                field.Type,
                field.IsRequired,
                field.Order,
                field.ModuleConfig.HasValue ? JsonSerializer.Serialize(field.ModuleConfig.Value) : null,
                request.DefinitionSnapshot.CreatedAt)).ToList());
    }
}

internal static class ReportingPhotoTypeLabels
{
    private static readonly ResourceManager Resources =
        new("LgymApi.Resources.Resources.Enums", typeof(LgymApi.Resources.Enums).Assembly);

    public static PhotoTypeLabelsResult Resolve(TrainerPhotoTypePersistenceModel photoType)
    {
        if (photoType.NameMode != PhotoTypeNameMode.SystemLocalized || string.IsNullOrWhiteSpace(photoType.SystemKey))
        {
            return new PhotoTypeLabelsResult(photoType.Name, photoType.Name);
        }

        var resourceKey = $"TrainerPhotoType_{photoType.SystemKey}";
        return new PhotoTypeLabelsResult(
            Resources.GetString(resourceKey, CultureInfo.GetCultureInfo("pl")) ?? photoType.Name,
            Resources.GetString(resourceKey, CultureInfo.GetCultureInfo("en")) ?? photoType.Name);
    }

    public static string DisplayName(PhotoTypeLabelsResult labels)
        => string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "pl", StringComparison.OrdinalIgnoreCase)
            ? labels.Pl
            : labels.En;
}
