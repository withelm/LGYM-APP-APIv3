namespace LgymApi.Domain.Enums;

/// <summary>
/// Represents the execution lifecycle status of a background action.
/// </summary>
public enum ActionExecutionStatus
{
    /// <summary>
    /// Action is queued and waiting to be processed.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Action is currently being executed.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Action completed successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Action failed and will be retried.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Action failed after all retry attempts and is permanently marked as dead-lettered.
    /// </summary>
    DeadLettered = 4
}
