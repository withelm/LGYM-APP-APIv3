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
        catch (AppException ex)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.StatusCode = ex.StatusCode;
            if (ex.Payload != null)
            {
                await context.Response.WriteAsJsonAsync(ex.Payload);
                return;
            }

            await context.Response.WriteAsJsonAsync(new ResponseMessageDto { Message = ex.Message });
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ResponseMessageDto { Message = Messages.TryAgain });
        }
    }
}
