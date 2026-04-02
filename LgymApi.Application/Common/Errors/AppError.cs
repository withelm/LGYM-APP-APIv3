using System.Net;

namespace LgymApi.Application.Common.Errors;

/// <summary>
/// Abstract base class for application-level errors with HTTP status code mapping.
/// </summary>
public abstract class AppError
{
    /// <summary>
    /// Gets the error message.
    /// </summary>
    public abstract string Message { get; }

    /// <summary>
    /// Gets the HTTP status code associated with this error.
    /// </summary>
    public abstract HttpStatusCode HttpStatusCode { get; }

    /// <summary>
    /// Gets the error payload, if any. Returns null by default.
    /// </summary>
    /// <returns>Error payload object or null.</returns>
    public virtual object? GetPayload() => null;
}

/// <summary>
/// Error indicating the requested resource was not found (HTTP 404).
/// </summary>
public sealed class NotFoundError : AppError
{
    private readonly string _message;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public NotFoundError(string message)
    {
        _message = message;
    }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public override string Message => _message;

    /// <summary>
    /// Gets the HTTP status code (404 Not Found).
    /// </summary>
    public override HttpStatusCode HttpStatusCode => HttpStatusCode.NotFound;
}

/// <summary>
/// Error indicating the request was malformed or invalid (HTTP 400).
/// </summary>
public sealed class BadRequestError : AppError
{
    private readonly string _message;
    private readonly object? _payload;

    /// <summary>
    /// Initializes a new instance of the <see cref="BadRequestError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="payload">Optional error payload (e.g., validation details).</param>
    public BadRequestError(string message, object? payload = null)
    {
        _message = message;
        _payload = payload;
    }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public override string Message => _message;

    /// <summary>
    /// Gets the HTTP status code (400 Bad Request).
    /// </summary>
    public override HttpStatusCode HttpStatusCode => HttpStatusCode.BadRequest;

    /// <summary>
    /// Gets the error payload.
    /// </summary>
    /// <returns>The error payload or null.</returns>
    public override object? GetPayload() => _payload;
}

/// <summary>
/// Error indicating the user is not authenticated (HTTP 401).
/// </summary>
public sealed class UnauthorizedError : AppError
{
    private readonly string _message;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnauthorizedError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public UnauthorizedError(string message)
    {
        _message = message;
    }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public override string Message => _message;

    /// <summary>
    /// Gets the HTTP status code (401 Unauthorized).
    /// </summary>
    public override HttpStatusCode HttpStatusCode => HttpStatusCode.Unauthorized;
}

/// <summary>
/// Error indicating the user does not have permission to perform the action (HTTP 403).
/// </summary>
public sealed class ForbiddenError : AppError
{
    private readonly string _message;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ForbiddenError(string message)
    {
        _message = message;
    }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public override string Message => _message;

    /// <summary>
    /// Gets the HTTP status code (403 Forbidden).
    /// </summary>
    public override HttpStatusCode HttpStatusCode => HttpStatusCode.Forbidden;
}

/// <summary>
/// Error indicating a conflict with the current state of the resource (HTTP 409).
/// </summary>
public sealed class ConflictError : AppError
{
    private readonly string _message;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConflictError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ConflictError(string message)
    {
        _message = message;
    }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public override string Message => _message;

    /// <summary>
    /// Gets the HTTP status code (409 Conflict).
    /// </summary>
    public override HttpStatusCode HttpStatusCode => HttpStatusCode.Conflict;
}
