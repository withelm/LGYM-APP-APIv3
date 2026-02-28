using LgymApi.BackgroundWorker.Common;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.BackgroundWorker;

/// <summary>
/// Default implementation of action execution scope provider.
/// Creates independent DI scopes for each action execution to ensure isolation in parallel execution.
/// </summary>
public class ActionExecutionScopeProvider : IActionExecutionScopeProvider
{
    private readonly IServiceProvider _serviceProvider;

    public ActionExecutionScopeProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Creates a new isolated scope for action execution.
    /// Each call returns a fresh IServiceScope with independent scoped service instances,
    /// including a new DbContext instance exclusive to this action execution.
    /// </summary>
    public IServiceScope CreateActionExecutionScope()
    {
        return _serviceProvider.CreateScope();
    }
}
