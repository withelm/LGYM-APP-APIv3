using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Application.Exceptions;
using LgymApi.Resources;

namespace LgymApi.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            // Handle AppException (from excluded infrastructure components like RouteUserAccessGuard)
            if (ex is AppException appEx)
            {
                _logger.LogError(ex, "Application exception");
                context.Response.StatusCode = appEx.StatusCode;
                var response = appEx.Payload ?? new ResponseMessageDto { Message = appEx.Message };
                await context.Response.WriteAsJsonAsync(response);
                return;
            }

            // Handle all other exceptions as 500 Internal Server Error
            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ResponseMessageDto { Message = Messages.TryAgain });
        }
    }
}
