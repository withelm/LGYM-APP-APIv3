using LgymApi.Domain.Enums;

namespace LgymApi.Domain.Entities;

/// <summary>
/// Durable command envelope for storing background action commands with full lifecycle tracking.
/// Ensures idempotency and traceability of command execution.
/// </summary>
public sealed class CommandEnvelope : EntityBase
{

    // Retry policy configuration
    public const int MaxRetryAttempts = 3;
    public static readonly int[] RetryDelaysSeconds = { 60, 300, 900 }; // 1m, 5m, 15m
    /// <summary>
    /// Unique correlation identifier to link related commands and logs.
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Serialized command payload as JSON.
    /// </summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>
    /// CLR type FullName of the command for exact-type resolution.
    /// Stored as durable string to survive app restarts.
    /// </summary>
    public string CommandTypeFullName { get; set; } = string.Empty;

    /// <summary>
    /// Current execution status of the envelope.
    /// </summary>
    public ActionExecutionStatus Status { get; set; } = ActionExecutionStatus.Pending;

    /// <summary>
    /// Timestamp when execution attempts should resume (for backoff/retry timing).
    /// Null if no next attempt is scheduled.
    /// </summary>
    public DateTimeOffset? NextAttemptAt { get; set; }

    /// <summary>
    /// Timestamp of the most recent execution attempt.
    /// </summary>
    public DateTimeOffset? LastAttemptAt { get; set; }

    /// <summary>
    /// Timestamp when the envelope was first successfully processed.
    /// Null until status reaches Completed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Related execution logs for this envelope (one-to-many).
    /// </summary>
    public ICollection<ActionExecutionLog> ExecutionLogs { get; set; } = new List<ActionExecutionLog>();

    /// <summary>
    /// Increments the attempt counter and records an error on this envelope.
    /// Updates LastAttemptAt and schedules the next retry based on attempt count.
    /// Does NOT mark as DeadLettered; caller must decide after this call.
    /// </summary>
    public void RecordAttemptFailure(string errorMessage, string? errorDetails = null)
    {
        if (Status == ActionExecutionStatus.DeadLettered)
        {
            throw new InvalidOperationException("Cannot record failure on dead-lettered envelope.");
        }

        // Increment attempt number for next retry logic
        var attemptNumber = ExecutionLogs.Count(log => log.ActionType == ActionExecutionLogType.Execute);

        // Record this failure in execution log
        var executionLog = new ActionExecutionLog
        {
            CommandEnvelopeId = Id,
            ActionType = ActionExecutionLogType.Execute,
            Status = ActionExecutionStatus.Failed,
            AttemptNumber = attemptNumber,
            ErrorMessage = errorMessage,
            ErrorDetails = errorDetails
        };
        ExecutionLogs.Add(executionLog);

        // Update envelope state
        Status = ActionExecutionStatus.Failed;
        LastAttemptAt = DateTimeOffset.UtcNow;

        // Schedule next attempt if we haven't exhausted all delay values
        if (attemptNumber < RetryDelaysSeconds.Length)
        {
            var delaySeconds = RetryDelaysSeconds[attemptNumber];
            NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);
        }
        else
        {
            // Last attempt failed; will be marked dead-lettered by caller
            NextAttemptAt = null;
        }
    }

    /// <summary>
    /// Marks the envelope as dead-lettered after max retry attempts exceeded.
    /// Preserves all error history. Terminal state - cannot be transitioned further.
    /// </summary>
    public void MarkDeadLettered()
    {
        if (Status == ActionExecutionStatus.DeadLettered)
        {
            return; // Already dead-lettered, idempotent
        }

        Status = ActionExecutionStatus.DeadLettered;
        LastAttemptAt = DateTimeOffset.UtcNow;
        NextAttemptAt = null;
        CompletedAt = DateTimeOffset.UtcNow;

        // Record dead-letter event in execution log
        var executionLog = new ActionExecutionLog
        {
            CommandEnvelopeId = Id,
            ActionType = ActionExecutionLogType.DeadLetter,
            Status = ActionExecutionStatus.DeadLettered,
            AttemptNumber = ExecutionLogs.Count(log => log.ActionType == ActionExecutionLogType.Execute),
            ErrorMessage = "Dead-lettered after maximum retry attempts exceeded",
            ErrorDetails = null
        };
        ExecutionLogs.Add(executionLog);
    }

    /// <summary>
    /// Marks the envelope as completed successfully.
    /// Preserves all prior failure history.
    /// </summary>
    public void MarkCompleted()
    {
        if (Status == ActionExecutionStatus.Completed || Status == ActionExecutionStatus.DeadLettered)
        {
            return; // Already terminal
        }

        Status = ActionExecutionStatus.Completed;
        LastAttemptAt = DateTimeOffset.UtcNow;
        CompletedAt = DateTimeOffset.UtcNow;
        NextAttemptAt = null;

        // Record success in execution log
        var executionLog = new ActionExecutionLog
        {
            CommandEnvelopeId = Id,
            ActionType = ActionExecutionLogType.Execute,
            Status = ActionExecutionStatus.Completed,
            AttemptNumber = ExecutionLogs.Count(log => log.ActionType == ActionExecutionLogType.Execute),
            ErrorMessage = null,
            ErrorDetails = null
        };
        ExecutionLogs.Add(executionLog);
    }

    /// <summary>
    /// Determines if this envelope should be retried based on attempt count and status.
    /// </summary>
    public bool ShouldRetry()
    {
        if (Status != ActionExecutionStatus.Failed)
        {
            return false;
        }

        var executionLogCount = ExecutionLogs.Count(log => log.ActionType == ActionExecutionLogType.Execute);
        return executionLogCount <= MaxRetryAttempts;
    }
}
