namespace LgymApi.Application.Common.Errors;

/// <summary>
/// Error indicating the requested ELO registry data was not found (HTTP 404).
/// </summary>
public sealed class EloRegistryNotFoundError : NotFoundError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EloRegistryNotFoundError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public EloRegistryNotFoundError(string message) : base(message)
    {
    }
}
