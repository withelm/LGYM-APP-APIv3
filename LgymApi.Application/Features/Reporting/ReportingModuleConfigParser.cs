using System.Text.Json;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Reporting;

public static class ReportingModuleConfigParser
{
    public static bool TryNormalizePhotoModuleConfig(
        JsonElement? moduleConfig,
        out JsonElement normalizedConfig,
        out IReadOnlyList<PhotoViewType> requiredViews)
    {
        normalizedConfig = default;
        requiredViews = [];

        if (!TryGetArrayProperty(moduleConfig, "requiredViews", out var requiredViewsElement))
        {
            return false;
        }

        var parsedViews = new List<PhotoViewType>();
        var seen = new HashSet<PhotoViewType>();

        foreach (var viewElement in requiredViewsElement.EnumerateArray())
        {
            if (viewElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var viewName = viewElement.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(viewName)
                || !System.Enum.TryParse<PhotoViewType>(viewName, true, out var viewType)
                || !System.Enum.IsDefined(viewType)
                || !seen.Add(viewType))
            {
                return false;
            }

            parsedViews.Add(viewType);
        }

        if (parsedViews.Count == 0)
        {
            return false;
        }

        requiredViews = parsedViews;
        normalizedConfig = JsonSerializer.SerializeToElement(new
        {
            requiredViews = parsedViews.Select(x => x.ToString()).ToArray()
        });

        return true;
    }

    public static bool TryNormalizeMeasurementModuleConfig(
        JsonElement? moduleConfig,
        out JsonElement normalizedConfig,
        out IReadOnlyList<BodyParts> measurementTypes)
    {
        normalizedConfig = default;
        measurementTypes = [];

        if (!TryGetArrayProperty(moduleConfig, "measurementTypes", out var measurementTypesElement))
        {
            return false;
        }

        var parsedTypes = new List<BodyParts>();
        var seen = new HashSet<BodyParts>();

        foreach (var measurementTypeElement in measurementTypesElement.EnumerateArray())
        {
            if (measurementTypeElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var bodyPartName = measurementTypeElement.GetString();
            if (string.IsNullOrWhiteSpace(bodyPartName)
                || !TryResolveBodyPart(bodyPartName, out var bodyPart)
                || bodyPart == BodyParts.Unknown
                || !seen.Add(bodyPart))
            {
                return false;
            }

            parsedTypes.Add(bodyPart);
        }

        if (parsedTypes.Count == 0)
        {
            return false;
        }

        measurementTypes = parsedTypes;
        normalizedConfig = JsonSerializer.SerializeToElement(new
        {
            measurementTypes = parsedTypes.Select(x => x.ToString()).ToArray()
        });

        return true;
    }

    private static bool TryResolveBodyPart(string bodyPartName, out BodyParts bodyPart)
    {
        var normalized = bodyPartName.Trim();
        switch (normalized.ToLowerInvariant())
        {
            case "weight":
                bodyPart = BodyParts.BodyWeight;
                return true;
            case "bodyfat":
                bodyPart = BodyParts.BodyFat;
                return true;
            case "thighs":
                bodyPart = BodyParts.Thigh;
                return true;
        }

        return System.Enum.TryParse(normalized, true, out bodyPart)
               && System.Enum.IsDefined(bodyPart);
    }

    private static bool TryGetArrayProperty(JsonElement? moduleConfig, string propertyName, out JsonElement arrayElement)
    {
        arrayElement = default;

        if (!moduleConfig.HasValue || moduleConfig.Value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return moduleConfig.Value.TryGetProperty(propertyName, out arrayElement)
               && arrayElement.ValueKind == JsonValueKind.Array;
    }
}
