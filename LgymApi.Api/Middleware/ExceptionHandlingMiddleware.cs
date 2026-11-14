using LgymApi.Resources;
using Microsoft.EntityFrameworkCore;
using Plan = LgymApi.Domain.Entities.Plan;

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
        catch (DbUpdateConcurrencyException ex) when (ex.Entries.Count > 0 && ex.Entries.All(entry => entry.Entity is Plan))
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            await ErrorResponseWriter.WriteAsync(context, StatusCodes.Status409Conflict, Messages.TryAgain, context.RequestAborted);
        }
        catch (UnauthorizedAccessException ex)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            await ErrorResponseWriter.WriteAsync(context, StatusCodes.Status403Forbidden, ex.Message, context.RequestAborted);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            // Handle all other exceptions as 500 Internal Server Error
            _logger.LogError(ex, "Unhandled exception");
            await ErrorResponseWriter.WriteAsync(context, StatusCodes.Status500InternalServerError, Messages.TryAgain, context.RequestAborted);
        }
    }
}
