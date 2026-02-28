namespace LgymApi.BackgroundWorker.Common;

/// <summary>
/// Generic interface for background action handlers.
/// Handlers must be strongly typed to process specific command types.
/// </summary>
/// <typeparam name="TCommand">The command type this handler processes. Must implement IActionCommand.</typeparam>
public interface IBackgroundAction<in TCommand>
    where TCommand : IActionCommand
{
    /// <summary>
    /// Executes the background action for the given command.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">Cancellation token to support graceful shutdown.</param>
    /// <returns>A task representing the asynchronous execution.</returns>
    Task ExecuteAsync(TCommand command, CancellationToken cancellationToken = default);
}
