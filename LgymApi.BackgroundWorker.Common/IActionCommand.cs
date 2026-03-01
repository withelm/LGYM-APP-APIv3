namespace LgymApi.BackgroundWorker.Common;

/// <summary>
/// Marker interface for strongly-typed background action commands.
/// Commands are routed by exact CLR type, never by string event name.
/// </summary>
public interface IActionCommand
{
}
