using System.Net;

namespace LgymApi.Application.Exceptions;

public class AppException : Exception
{
    public int StatusCode { get; }
    public object? Payload { get; }

    public AppException(string message, HttpStatusCode statusCode, object? payload = null)
        : base(message)
    {
        StatusCode = (int)statusCode;
        Payload = payload;
    }

    public static AppException BadRequest(string message, object? payload = null)
        => new(message, HttpStatusCode.BadRequest, payload);

    public static AppException Unauthorized(string message, object? payload = null)
        => new(message, HttpStatusCode.Unauthorized, payload);

    public static AppException Forbidden(string message, object? payload = null)
        => new(message, HttpStatusCode.Forbidden, payload);

    public static AppException NotFound(string message, object? payload = null)
        => new(message, HttpStatusCode.NotFound, payload);

    public static AppException Conflict(string message, object? payload = null)
        => new(message, HttpStatusCode.Conflict, payload);

    public static AppException Internal(string message, object? payload = null)
        => new(message, HttpStatusCode.InternalServerError, payload);
}
