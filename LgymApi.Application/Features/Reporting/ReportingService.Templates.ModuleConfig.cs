using System.Text.Json;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Features.Reporting;

public sealed partial class ReportingService
{
    private static bool TryReadRequiredPhotoViews(JsonElement? moduleConfig, out HashSet<PhotoViewType> requiredViews)
    {
        requiredViews = [];
        if (!TryGetArrayProperty(moduleConfig, "requiredViews", out var requiredViewsElement))
        {
            return false;
        }

        foreach (var viewElement in requiredViewsElement.EnumerateArray())
        {
            if (viewElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var viewName = viewElement.GetString();
            if (string.IsNullOrWhiteSpace(viewName)
                || !System.Enum.TryParse<PhotoViewType>(viewName.Trim(), true, out var viewType)
                || !System.Enum.IsDefined(viewType))
            {
                return false;
            }

            requiredViews.Add(viewType);
        }

        return requiredViews.Count > 0;
    }

    private static bool TryReadMeasurementTypes(JsonElement? moduleConfig, out HashSet<BodyParts> measurementTypes)
    {
        measurementTypes = [];
        if (!TryGetArrayProperty(moduleConfig, "measurementTypes", out var measurementTypesElement))
        {
            return false;
        }

        foreach (var measurementTypeElement in measurementTypesElement.EnumerateArray())
        {
            if (measurementTypeElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var bodyPartName = measurementTypeElement.GetString();
            if (string.IsNullOrWhiteSpace(bodyPartName)
                || !TryResolveBodyPart(bodyPartName, out var bodyPart)
                || bodyPart == BodyParts.Unknown)
            {
                return false;
            }

            measurementTypes.Add(bodyPart);
        }

        return measurementTypes.Count > 0;
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

        var config = moduleConfig.Value;
        return config.TryGetProperty(propertyName, out arrayElement)
               && arrayElement.ValueKind == JsonValueKind.Array;
    }
}
