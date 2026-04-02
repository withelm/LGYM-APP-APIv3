using System.Net;

namespace LgymApi.Application.Common.Errors;

/// <summary>
/// Error indicating the requested exercise score was not found (HTTP 404).
/// </summary>
public sealed class ExerciseScoreNotFoundError : AppError
{
    private readonly string _message;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExerciseScoreNotFoundError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ExerciseScoreNotFoundError(string message)
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
