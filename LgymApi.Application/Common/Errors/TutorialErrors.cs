using System.Net;

namespace LgymApi.Application.Common.Errors;

/// <summary>
/// Error indicating an invalid user ID was provided (HTTP 400).
/// </summary>
public sealed class InvalidUserIdError : AppError
{
    private readonly string _message;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidUserIdError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public InvalidUserIdError(string message)
    {
        _message = message;
    }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public override string Message => _message;

    /// <summary>
    /// Gets the HTTP status code (400 Bad Request).
    /// </summary>
    public override HttpStatusCode HttpStatusCode => HttpStatusCode.BadRequest;
}

/// <summary>
/// Error indicating an invalid tutorial type was provided (HTTP 400).
/// </summary>
public sealed class InvalidTutorialTypeError : AppError
{
    private readonly string _message;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidTutorialTypeError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public InvalidTutorialTypeError(string message)
    {
        _message = message;
    }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public override string Message => _message;

    /// <summary>
    /// Gets the HTTP status code (400 Bad Request).
    /// </summary>
    public override HttpStatusCode HttpStatusCode => HttpStatusCode.BadRequest;
}

/// <summary>
/// Error indicating an invalid tutorial step was provided (HTTP 400).
/// </summary>
public sealed class InvalidTutorialStepError : AppError
{
    private readonly string _message;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidTutorialStepError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public InvalidTutorialStepError(string message)
    {
        _message = message;
    }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public override string Message => _message;

    /// <summary>
    /// Gets the HTTP status code (400 Bad Request).
    /// </summary>
    public override HttpStatusCode HttpStatusCode => HttpStatusCode.BadRequest;
}

/// <summary>
/// Error indicating the requested tutorial progress was not found (HTTP 404).
/// </summary>
public sealed class TutorialProgressNotFoundError : AppError
{
    private readonly string _message;

    /// <summary>
    /// Initializes a new instance of the <see cref="TutorialProgressNotFoundError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public TutorialProgressNotFoundError(string message)
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
