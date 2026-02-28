using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.BackgroundWorker.Common;

/// <summary>
/// Provides isolated DI scopes for per-action execution in parallel fan-out scenarios.
/// Each action receives an independent scoped service container with unique DbContext instance.
/// </summary>
public interface IActionExecutionScopeProvider
{
    /// <summary>
    /// Creates a new isolated scope for action execution.
    /// Each call returns a fresh IServiceScope with independent scoped service instances,
    /// including a new DbContext instance exclusive to this action execution.
    /// </summary>
    IServiceScope CreateActionExecutionScope();
}
