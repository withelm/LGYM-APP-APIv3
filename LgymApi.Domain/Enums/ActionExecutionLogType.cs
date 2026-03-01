namespace LgymApi.Domain.Enums;

/// <summary>
/// Represents the kind of execution-log entry recorded for a command envelope.
/// </summary>
public enum ActionExecutionLogType
{
    /// <summary>
    /// Envelope-level execution attempt.
    /// </summary>
    Execute = 0,

    /// <summary>
    /// Single handler execution within orchestration fan-out.
    /// </summary>
    HandlerExecution = 1,

    /// <summary>
    /// Explicit retry lifecycle event.
    /// </summary>
    Retry = 2,

    /// <summary>
    /// Terminal dead-letter lifecycle event.
    /// </summary>
    DeadLetter = 3
}
