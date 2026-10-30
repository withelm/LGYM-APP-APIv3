using System.Text.Json;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Infrastructure.Reporting.PhotoTypesMigration;

internal sealed partial class ReportingPhotoTypesMigrationPlanner
{
    private static JsonElement? ParseModuleConfig(
        ReportTemplateField field,
        Id<ReportRequest> requestId,
        ReportingPhotoTypesMigrationReport report,
        ref bool unsafeRequest)
    {
        if (string.IsNullOrWhiteSpace(field.ModuleConfig))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(field.ModuleConfig, SharedSerializationOptions.Current);
        }
        catch (JsonException)
        {
            AddRequestError(report, requestId, "MODULE_CONFIG_MALFORMED", $"Field {field.Id} has malformed JSON; no snapshot was written.");
            unsafeRequest = true;
            return null;
        }
    }

    private static JsonElement? BuildPhotoModuleConfig(
        ReportRequest request,
        ReportTemplateField field,
        JsonElement config,
        IReadOnlyDictionary<(Id<User> TrainerId, string SystemKey), List<TrainerPhotoType>> existingTypes,
        IReadOnlyDictionary<(Id<User> TrainerId, string SystemKey), TrainerPhotoType> sharedPlannedTypes,
        IDictionary<(Id<User> TrainerId, string SystemKey), TrainerPhotoType> localTypes,
        IDictionary<string, List<MigrationPhotoBindingDocument>> bindingsByView,
        bool historyOnly,
        ReportingPhotoTypesMigrationReport report,
        ref bool unsafeRequest)
    {
        if (config.ValueKind != JsonValueKind.Object)
        {
            AddRequestError(report, request.Id, "PHOTO_CONFIG_INVALID", $"Photo field {field.Id} configuration is not an object.");
            unsafeRequest = true;
            return config;
        }

        var hasLegacyViews = config.TryGetProperty("requiredViews", out var requiredViews);
        var hasPhotoTypeIds = config.TryGetProperty("requiredPhotoTypeIds", out var requiredPhotoTypeIds);
        if (hasLegacyViews == hasPhotoTypeIds)
        {
            AddRequestError(report, request.Id, "PHOTO_CONFIG_AMBIGUOUS", $"Photo field {field.Id} must contain exactly one supported source definition form.");
            unsafeRequest = true;
            return config;
        }

        var bindings = hasLegacyViews
            ? ResolveLegacyBindings(request, field, requiredViews, existingTypes, sharedPlannedTypes, localTypes, historyOnly, report, ref unsafeRequest)
            : ResolveCurrentBindings(request, field, requiredPhotoTypeIds, existingTypes, sharedPlannedTypes, localTypes, report, ref unsafeRequest);
        if (unsafeRequest)
        {
            return config;
        }

        foreach (var binding in bindings)
        {
            if (!string.IsNullOrWhiteSpace(binding.SystemKey))
            {
                bindingsByView.GetOrAdd(binding.SystemKey!, static () => []).Add(binding);
            }
        }

        return JsonSerializer.SerializeToElement(new MigrationPhotoModuleConfig(
            2,
            bindings.Select(binding => binding.PhotoTypeId).ToList(),
            bindings));
    }

    private static List<MigrationPhotoBindingDocument> ResolveLegacyBindings(
        ReportRequest request,
        ReportTemplateField field,
        JsonElement requiredViews,
        IReadOnlyDictionary<(Id<User> TrainerId, string SystemKey), List<TrainerPhotoType>> existingTypes,
        IReadOnlyDictionary<(Id<User> TrainerId, string SystemKey), TrainerPhotoType> sharedPlannedTypes,
        IDictionary<(Id<User> TrainerId, string SystemKey), TrainerPhotoType> localTypes,
        bool historyOnly,
        ReportingPhotoTypesMigrationReport report,
        ref bool unsafeRequest)
    {
        var bindings = new List<MigrationPhotoBindingDocument>();
        if (requiredViews.ValueKind != JsonValueKind.Array)
        {
            AddRequestError(report, request.Id, "LEGACY_VIEWS_INVALID", $"Photo field {field.Id} requiredViews is not an array.");
            unsafeRequest = true;
            return bindings;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in requiredViews.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String
                || !LegacyPhotoTypeCatalog.TryResolve(element.GetString(), out var systemKey, out var labels)
                || !seen.Add(systemKey))
            {
                AddRequestError(report, request.Id, "LEGACY_VIEW_UNKNOWN", $"Photo field {field.Id} contains an unknown or duplicate legacy view.");
                unsafeRequest = true;
                continue;
            }

            var photoType = ResolveLegacyPhotoType(
                request,
                field,
                systemKey,
                labels,
                existingTypes,
                sharedPlannedTypes,
                localTypes,
                historyOnly,
                report,
                ref unsafeRequest);
            if (photoType is null)
            {
                continue;
            }

            bindings.Add(new MigrationPhotoBindingDocument(
                photoType.Id.ToString(),
                DeterministicMigrationId.Slot("slot", request.Id.ToString(), field.Id.ToString(), systemKey),
                systemKey,
                PhotoTypeNameMode.SystemLocalized,
                labels));
        }

        if (bindings.Count == 0)
        {
            AddRequestError(report, request.Id, "LEGACY_VIEWS_EMPTY", $"Photo field {field.Id} has no resolvable legacy views.");
            unsafeRequest = true;
        }
        return bindings;
    }

    private static TrainerPhotoType? ResolveLegacyPhotoType(
        ReportRequest request,
        ReportTemplateField field,
        string systemKey,
        MigrationPhotoTypeLabels labels,
        IReadOnlyDictionary<(Id<User> TrainerId, string SystemKey), List<TrainerPhotoType>> existingTypes,
        IReadOnlyDictionary<(Id<User> TrainerId, string SystemKey), TrainerPhotoType> sharedPlannedTypes,
        IDictionary<(Id<User> TrainerId, string SystemKey), TrainerPhotoType> localTypes,
        bool historyOnly,
        ReportingPhotoTypesMigrationReport report,
        ref bool unsafeRequest)
    {
        var key = (request.TrainerId, systemKey);
        if (existingTypes.TryGetValue(key, out var matches))
        {
            if (matches.Count != 1 || matches[0].IsDeleted)
            {
                AddRequestError(report, request.Id, "DEFAULT_DEFINITION_UNSAFE", $"Built-in {systemKey} has duplicate or deleted definitions for the original report trainer.");
                unsafeRequest = true;
                return null;
            }
            report.Counters.ExistingDefaultsPreserved++;
            return matches[0];
        }

        if (localTypes.TryGetValue(key, out var localType))
        {
            if (field.IsRequired && !historyOnly)
            {
                localType.IsArchived = false;
            }
            return localType;
        }
        if (sharedPlannedTypes.TryGetValue(key, out var sharedType))
        {
            if (field.IsRequired && !historyOnly)
            {
                sharedType.IsArchived = false;
            }
            return sharedType;
        }

        var created = new TrainerPhotoType
        {
            Id = DeterministicMigrationId.For<TrainerPhotoType>("default-photo-type", request.TrainerId.ToString(), systemKey),
            TrainerId = request.TrainerId,
            SystemKey = systemKey,
            Name = labels.En,
            NormalizedName = labels.En.Trim().ToUpperInvariant(),
            NameMode = PhotoTypeNameMode.SystemLocalized,
            IsArchived = historyOnly || !field.IsRequired
        };
        localTypes.Add(key, created);
        return created;
    }

    private static List<MigrationPhotoBindingDocument> ResolveCurrentBindings(
        ReportRequest request,
        ReportTemplateField field,
        JsonElement requiredPhotoTypeIds,
        IReadOnlyDictionary<(Id<User> TrainerId, string SystemKey), List<TrainerPhotoType>> existingTypes,
        IReadOnlyDictionary<(Id<User> TrainerId, string SystemKey), TrainerPhotoType> sharedPlannedTypes,
        IDictionary<(Id<User> TrainerId, string SystemKey), TrainerPhotoType> localTypes,
        ReportingPhotoTypesMigrationReport report,
        ref bool unsafeRequest)
    {
        _ = sharedPlannedTypes;
        _ = localTypes;
        var bindings = new List<MigrationPhotoBindingDocument>();
        if (requiredPhotoTypeIds.ValueKind != JsonValueKind.Array)
        {
            AddRequestError(report, request.Id, "PHOTO_TYPE_IDS_INVALID", $"Photo field {field.Id} requiredPhotoTypeIds is not an array.");
            unsafeRequest = true;
            return bindings;
        }

        var byId = existingTypes.Values.SelectMany(value => value).ToDictionary(photoType => photoType.Id);
        var seen = new HashSet<Id<TrainerPhotoType>>();
        foreach (var element in requiredPhotoTypeIds.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String
                || !Id<TrainerPhotoType>.TryParse(element.GetString() ?? string.Empty, out var photoTypeId)
                || !seen.Add(photoTypeId)
                || !byId.TryGetValue(photoTypeId, out var photoType)
                || photoType.TrainerId != request.TrainerId
                || photoType.IsDeleted)
            {
                AddRequestError(report, request.Id, "PHOTO_TYPE_DEFINITION_MISSING", $"Photo field {field.Id} references a missing, deleted, duplicate, or foreign photo type.");
                unsafeRequest = true;
                continue;
            }

            var labels = LegacyPhotoTypeCatalog.CurrentLabels(photoType.SystemKey, photoType.Name, photoType.NameMode);
            bindings.Add(new MigrationPhotoBindingDocument(
                photoType.Id.ToString(),
                DeterministicMigrationId.Slot("slot", request.Id.ToString(), field.Id.ToString(), photoType.Id.ToString()),
                photoType.SystemKey,
                photoType.NameMode,
                labels));
        }

        if (bindings.Count == 0)
        {
            AddRequestError(report, request.Id, "PHOTO_TYPE_IDS_EMPTY", $"Photo field {field.Id} has no resolvable photo type IDs.");
            unsafeRequest = true;
        }
        return bindings;
    }
}
