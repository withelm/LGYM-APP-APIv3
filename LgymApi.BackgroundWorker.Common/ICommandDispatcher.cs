namespace LgymApi.BackgroundWorker.Common;

/// <summary>
/// Strongly-typed command dispatcher for background job execution.
/// Provides type-safe enqueuing with retry policies and dead-letter handling.
/// </summary>
public interface ICommandDispatcher
{
    /// <summary>
    /// Enqueues a strongly-typed command for background execution.
    /// </summary>
    /// <typeparam name="TCommand">The command type (must implement IActionCommand).</typeparam>
    /// <param name="command">The command instance to enqueue.</param>
    void Enqueue<TCommand>(TCommand command)
        where TCommand : class, IActionCommand;
}
