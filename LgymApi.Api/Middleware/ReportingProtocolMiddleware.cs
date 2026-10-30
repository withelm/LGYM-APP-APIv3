namespace LgymApi.Api.Middleware;

public sealed class ReportingProtocolMiddleware
{
    public const string HeaderName = "X-LGYM-Reporting-Protocol";

    private readonly RequestDelegate _next;

    public ReportingProtocolMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var metadata = ReportingProtocolMetadata.Resolve(context.GetEndpoint());
        if (metadata is null)
        {
            await _next(context);
            return;
        }

        if (!TryResolveVersion(context.Request, out var version))
        {
            await ReportingProtocolResponses.WriteAsync(
                context,
                StatusCodes.Status400BadRequest,
                ReportingProtocolResponses.UnsupportedCode,
                LgymApi.Resources.Messages.ReportingProtocolUnsupported);
            return;
        }

        if (version < metadata.MinimumVersion)
        {
            await ReportingProtocolResponses.WriteAsync(
                context,
                StatusCodes.Status426UpgradeRequired,
                ReportingProtocolResponses.V2RequiredCode,
                LgymApi.Resources.Messages.ReportingProtocolV2Required);
            return;
        }

        var inspection = await ReportingProtocolRequestInspector.InspectAsync(context, metadata.RequestKind, version);
        if (inspection != ReportingProtocolRequestInspection.Compatible)
        {
            await RejectInspectionAsync(context, inspection);
            return;
        }

        ReportingProtocolContext.SetVersion(context, version);
        ApplyProtocolHeaders(context.Response, version);
        context.Response.OnStarting(() =>
        {
            ApplyProtocolHeaders(context.Response, version);
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private static bool TryResolveVersion(HttpRequest request, out int version)
    {
        if (!request.Headers.TryGetValue(HeaderName, out var values))
        {
            version = 1;
            return true;
        }

        if (values.Count == 1 && values[0] is "1" or "2")
        {
            version = values[0]![0] - '0';
            return true;
        }

        version = 0;
        return false;
    }

    private static Task RejectInspectionAsync(HttpContext context, ReportingProtocolRequestInspection inspection) => inspection switch
    {
        ReportingProtocolRequestInspection.RequiresV2 => ReportingProtocolResponses.WriteAsync(
            context,
            StatusCodes.Status400BadRequest,
            ReportingProtocolResponses.V2RequiredCode,
            LgymApi.Resources.Messages.ReportingProtocolV2Required),
        ReportingProtocolRequestInspection.PhotoTypeRequired => ReportingProtocolResponses.WriteAsync(
            context,
            StatusCodes.Status400BadRequest,
            ReportingProtocolResponses.PhotoTypeRequiredCode,
            LgymApi.Resources.Messages.ReportingProtocolPhotoTypeRequired),
        ReportingProtocolRequestInspection.PayloadTooLarge => ReportingProtocolResponses.WriteAsync(
            context,
            StatusCodes.Status413PayloadTooLarge,
            ReportingProtocolResponses.PayloadTooLargeCode,
            LgymApi.Resources.Messages.ReportingProtocolPayloadTooLarge),
        _ => throw new ArgumentOutOfRangeException(nameof(inspection), inspection, null)
    };

    internal static void ApplyProtocolHeaders(HttpResponse response, int version)
    {
        response.Headers[HeaderName] = version.ToString(System.Globalization.CultureInfo.InvariantCulture);
        response.Headers.CacheControl = "private, no-store";

        var varyValues = response.Headers.Vary
            .SelectMany(value => value?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (varyValues.Add(HeaderName))
        {
            response.Headers.Vary = string.Join(", ", varyValues);
        }
    }
}
