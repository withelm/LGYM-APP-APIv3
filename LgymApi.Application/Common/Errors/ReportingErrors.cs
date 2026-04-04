namespace LgymApi.Application.Common.Errors;

/// <summary>
/// Error indicating the requested reporting resource was not found (HTTP 404).
/// </summary>
public sealed class ReportingNotFoundError : NotFoundError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportingNotFoundError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ReportingNotFoundError(string message) : base(message)
    {
    }
}

/// <summary>
/// Error indicating invalid reporting data was provided (HTTP 400).
/// </summary>
public sealed class InvalidReportingError : BadRequestError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidReportingError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public InvalidReportingError(string message) : base(message)
    {
    }
}

/// <summary>
/// Error indicating insufficient permissions for reporting operations (HTTP 403).
/// </summary>
public sealed class ReportingForbiddenError : ForbiddenError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportingForbiddenError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ReportingForbiddenError(string message) : base(message)
    {
    }
}
