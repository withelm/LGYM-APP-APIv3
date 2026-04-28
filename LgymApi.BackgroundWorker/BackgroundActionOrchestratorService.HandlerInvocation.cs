using System.Collections.Concurrent;
using System.Linq.Expressions;
using LgymApi.BackgroundWorker.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker;

public sealed partial class BackgroundActionOrchestratorService
{
    // Delegate type for cached handler invocation
    private delegate Task HandlerInvoker(object handler, object command, CancellationToken cancellationToken);
    
    // Cache compiled invokers per command type (setup-time reflection, execution-time cached delegates)
    private static readonly ConcurrentDictionary<Type, HandlerInvoker> _invokerCache = new();

    /// <summary>
    /// Executes a single handler in an isolated scope.
    /// Resolves handler instance from scope to ensure isolated scoped dependencies.
    /// Returns execution result with success flag and error details.
    /// </summary>
    private async Task<HandlerExecutionResult> ExecuteHandlerInIsolatedScopeAsync(
        Type handlerType,
        object command,
        Type commandType,
        string expectedHandlerTypeName,
        CancellationToken cancellationToken)
    {
        var resolvedHandlerTypeName = expectedHandlerTypeName;
        try
        {
            // Create isolated scope for this handler
            using var scope = _serviceProvider.CreateScope();

            // Resolve handler instance from this scope (ensures isolated scoped dependencies)
            var handlers = scope.ServiceProvider.GetServices(handlerType).ToList();
            var handler = handlers.FirstOrDefault(h => h?.GetType().FullName == expectedHandlerTypeName)
                ?? throw new InvalidOperationException(
                    $"Handler with type '{expectedHandlerTypeName}' not found in execution scope. Available handlers: [{string.Join(", ", handlers.Select(h => h?.GetType().FullName ?? "null"))}]");
            resolvedHandlerTypeName = handler.GetType().FullName ?? expectedHandlerTypeName;

            // Get or create cached invoker delegate for this command type
            var invoker = _invokerCache.GetOrAdd(commandType, static cmdType =>
            {
                // Build expression: (handler, cmd, ct) => ((IBackgroundAction<TCommand>)handler).ExecuteAsync((TCommand)cmd, ct)
                var handlerParam = Expression.Parameter(typeof(object), "handler");
                var commandParam = Expression.Parameter(typeof(object), "command");
                var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

                var interfaceType = typeof(IBackgroundAction<>).MakeGenericType(cmdType);
                var executeMethod = interfaceType.GetMethod(nameof(IBackgroundAction<IActionCommand>.ExecuteAsync))
                    ?? throw new InvalidOperationException($"ExecuteAsync method not found on {interfaceType.FullName}");

                var handlerCast = Expression.Convert(handlerParam, interfaceType);
                var commandCast = Expression.Convert(commandParam, cmdType);
                var methodCall = Expression.Call(handlerCast, executeMethod, commandCast, ctParam);

                return Expression.Lambda<HandlerInvoker>(methodCall, handlerParam, commandParam, ctParam).Compile();
            });

            // Execute via cached compiled delegate (no MethodInfo.Invoke)
            await invoker(handler, command, cancellationToken);

            // Persist any EF changes staged by the handler in this isolated scope.
            await CommitHandlerScopeAsync(scope, cancellationToken);

            _logger.LogInformation(
                "Handler {HandlerType} executed successfully for command {CommandType}.",
                resolvedHandlerTypeName,
                commandType.FullName);

            return new HandlerExecutionResult
            {
                Success = true,
                HandlerTypeName = resolvedHandlerTypeName
            };
        }
        catch (Exception ex)
        {
            // Exceptions from compiled delegates are direct (no TargetInvocationException wrapping)
            var inner = ex;

            _logger.LogError(ex,
                "Handler {HandlerType} failed for command {CommandType}.",
                resolvedHandlerTypeName,
                commandType.FullName);

            return new HandlerExecutionResult
            {
                Success = false,
                ErrorMessage = $"{resolvedHandlerTypeName}: {inner.Message}",
                HandlerTypeName = resolvedHandlerTypeName,
                ErrorDetails = inner.ToString() // Full exception with stack trace
            };
        }
    }
}
