using LgymApi.Domain.Enums;

namespace LgymApi.Domain.Entities;

/// <summary>
/// Execution log for tracking per-action execution attempts and results.
/// Each action performed on a CommandEnvelope generates an ExecutionLog entry.
/// </summary>
public sealed class ExecutionLog : EntityBase
{
    /// <summary>
    /// Foreign key to the CommandEnvelope this log entry belongs to.
    /// </summary>
    public Guid CommandEnvelopeId { get; set; }

    /// <summary>
    /// Navigation property to the parent CommandEnvelope.
    /// </summary>
    public CommandEnvelope CommandEnvelope { get; set; } = null!;

    /// <summary>
    /// Action type discriminator (e.g., "Dispatch", "Execute", "Retry", "DeadLetter").
    /// Stored as string for extensibility without schema changes.
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Execution status resulting from this action attempt.
    /// </summary>
    public ActionExecutionStatus Status { get; set; }

    /// <summary>
    /// Attempt count at time this log was created (0 for first attempt, increments per retry).
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Error message if the action failed, null on success.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Full error details or stack trace for debugging, null if no error.
    /// </summary>
    public string? ErrorDetails { get; set; }
}
