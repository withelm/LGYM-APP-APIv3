using LgymApi.Api.Features.Trainer.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Middleware;

internal static class ReportingProtocolResponses
{
    public const string UnsupportedCode = "REPORTING_PROTOCOL_UNSUPPORTED";
    public const string V2DisabledCode = "REPORTING_PROTOCOL_V2_DISABLED";
    public const string V2RequiredCode = "REPORTING_PROTOCOL_V2_REQUIRED";
    public const string UpgradeRequiredCode = "REPORTING_PROTOCOL_UPGRADE_REQUIRED";
    public const string PhotoTypeRequiredCode = "REPORTING_PROTOCOL_PHOTO_TYPE_REQUIRED";
    public const string PayloadTooLargeCode = "REPORTING_PROTOCOL_PAYLOAD_TOO_LARGE";

    public static ObjectResult UpgradeRequiredResult() => new(
        new ReportingProtocolErrorResponse
        {
            Message = LgymApi.Resources.Messages.ReportingProtocolUpgradeRequired,
            Code = UpgradeRequiredCode
        })
    {
        StatusCode = StatusCodes.Status426UpgradeRequired
    };

    public static Task WriteAsync(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status;
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers.Vary = ReportingProtocolMiddleware.HeaderName;
        return context.Response.WriteAsJsonAsync(
            new ReportingProtocolErrorResponse { Message = message, Code = code },
            context.RequestAborted);
    }
}
