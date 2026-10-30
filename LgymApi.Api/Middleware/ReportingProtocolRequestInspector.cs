using System.Text.Json;

namespace LgymApi.Api.Middleware;

internal enum ReportingProtocolRequestInspection
{
    Compatible,
    RequiresV2,
    PhotoTypeRequired,
    PayloadTooLarge
}

internal static class ReportingProtocolRequestInspector
{
    private const int BufferThresholdBytes = 64 * 1024;
    private const int BufferLimitBytes = 1024 * 1024;

    public static async Task<ReportingProtocolRequestInspection> InspectAsync(
        HttpContext context,
        ReportingProtocolRequestKind requestKind,
        int version)
    {
        if (requestKind == ReportingProtocolRequestKind.None || !RequestCanHaveBody(context.Request.Method))
        {
            return ReportingProtocolRequestInspection.Compatible;
        }

        if (context.Request.ContentLength > BufferLimitBytes)
        {
            return ReportingProtocolRequestInspection.PayloadTooLarge;
        }

        context.Request.EnableBuffering(BufferThresholdBytes, BufferLimitBytes);
        try
        {
            context.Request.Body.Position = 0;
            using var document = await JsonDocument.ParseAsync(
                context.Request.Body,
                cancellationToken: context.RequestAborted);
            return requestKind switch
            {
                ReportingProtocolRequestKind.Template => InspectTemplate(document.RootElement, version),
                ReportingProtocolRequestKind.Upload => InspectUpload(document.RootElement, version),
                _ => ReportingProtocolRequestInspection.Compatible
            };
        }
        catch (IOException)
        {
            return ReportingProtocolRequestInspection.PayloadTooLarge;
        }
        catch (JsonException)
        {
            return ReportingProtocolRequestInspection.Compatible;
        }
        finally
        {
            if (context.Request.Body.CanSeek)
            {
                context.Request.Body.Position = 0;
            }
        }
    }

    private static ReportingProtocolRequestInspection InspectTemplate(JsonElement root, int version)
    {
        if (version == 2 || root.ValueKind != JsonValueKind.Object)
        {
            return ReportingProtocolRequestInspection.Compatible;
        }

        if (HasSchemaVersionTwo(root))
        {
            return ReportingProtocolRequestInspection.RequiresV2;
        }

        if (!TryGetProperty(root, "fields", out var fields) || fields.ValueKind != JsonValueKind.Array)
        {
            return ReportingProtocolRequestInspection.Compatible;
        }

        foreach (var field in fields.EnumerateArray())
        {
            if (field.ValueKind == JsonValueKind.Object
                && TryGetProperty(field, "moduleConfig", out var moduleConfig)
                && moduleConfig.ValueKind == JsonValueKind.Object
                && (HasSchemaVersionTwo(moduleConfig) || TryGetProperty(moduleConfig, "requiredPhotoTypeIds", out _)))
            {
                return ReportingProtocolRequestInspection.RequiresV2;
            }
        }

        return ReportingProtocolRequestInspection.Compatible;
    }

    private static ReportingProtocolRequestInspection InspectUpload(JsonElement root, int version)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return ReportingProtocolRequestInspection.Compatible;
        }

        var hasPhotoType = TryGetProperty(root, "photoTypeId", out var photoTypeId);
        var hasSlot = TryGetProperty(root, "reportPhotoSlotId", out _);
        if (version == 1)
        {
            return hasPhotoType || hasSlot
                ? ReportingProtocolRequestInspection.RequiresV2
                : ReportingProtocolRequestInspection.Compatible;
        }

        return hasPhotoType
               && photoTypeId.ValueKind == JsonValueKind.String
               && !string.IsNullOrWhiteSpace(photoTypeId.GetString())
            ? ReportingProtocolRequestInspection.Compatible
            : ReportingProtocolRequestInspection.PhotoTypeRequired;
    }

    private static bool HasSchemaVersionTwo(JsonElement value) =>
        TryGetProperty(value, "schemaVersion", out var schemaVersion)
        && schemaVersion.ValueKind == JsonValueKind.Number
        && schemaVersion.TryGetInt32(out var version)
        && version == 2;

    private static bool TryGetProperty(JsonElement value, string name, out JsonElement property)
    {
        foreach (var candidate in value.EnumerateObject())
        {
            if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }

    private static bool RequestCanHaveBody(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method);
}
