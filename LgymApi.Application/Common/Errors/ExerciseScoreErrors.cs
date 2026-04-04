namespace LgymApi.Application.Common.Errors;

/// <summary>
/// Error indicating the requested exercise score was not found (HTTP 404).
/// </summary>
public sealed class ExerciseScoreNotFoundError : NotFoundError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExerciseScoreNotFoundError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ExerciseScoreNotFoundError(string message) : base(message)
    {
    }
}
