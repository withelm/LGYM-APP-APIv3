using LgymApi.Api.Features.Common.Contracts;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace LgymApi.Api.Middleware;

public static class ErrorResponseWriter
{
    public static async Task WriteAsync(HttpContext context, int statusCode, string message, CancellationToken cancellationToken = default)
    {
        if (context.Response.HasStarted)
        {
            var loggerFactory = context.RequestServices.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger(nameof(ErrorResponseWriter));
            logger?.LogWarning("Attempted to write error response after response has started.");
            return;
        }

        context.Response.StatusCode = statusCode;

        var jsonOptions = context.RequestServices
            .GetRequiredService<IOptions<JsonOptions>>()
            .Value.SerializerOptions;

        await context.Response.WriteAsJsonAsync(
            new ResponseMessageDto { Message = message },
            jsonOptions,
            cancellationToken);
    }
}
