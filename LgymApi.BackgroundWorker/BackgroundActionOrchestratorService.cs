using System.Text.Json;
using System.Reflection;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker;

/// <summary>
/// Core orchestrator handler for background action commands.
/// Loads durable command envelope, resolves exact-type handlers, executes actions in parallel with isolated scopes.
/// Implements continue-on-error, retry/backoff, and dead-letter policies.
/// </summary>
public sealed class BackgroundActionOrchestratorService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICommandEnvelopeRepository _commandEnvelopeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BackgroundActionOrchestratorService> _logger;

    // Parallel execution configuration
    private const int MaxDegreeOfParallelism = 4;

    public BackgroundActionOrchestratorService(
        IServiceProvider serviceProvider,
        ICommandEnvelopeRepository commandEnvelopeRepository,
        IUnitOfWork unitOfWork,
        ILogger<BackgroundActionOrchestratorService> logger)
    {
        _serviceProvider = serviceProvider;
        _commandEnvelopeRepository = commandEnvelopeRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Orchestrates background action execution for a single command envelope.
    /// </summary>
    /// <param name="envelopeId">Durable command envelope id</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task OrchestrateAsync(Guid envelopeId, CancellationToken cancellationToken = default)
    {
        // Load envelope with execution logs
        var envelope = await _commandEnvelopeRepository.FindByIdAsync(envelopeId, cancellationToken);
        if (envelope == null)
        {
            _logger.LogWarning(
                "Command envelope {EnvelopeId} not found. Skipping orchestration.",
                envelopeId);
            return;
        }

        // Skip if already terminal
        if (envelope.Status == ActionExecutionStatus.Completed)
        {
            _logger.LogInformation(
                "Command envelope {EnvelopeId} already completed. Skipping duplicate orchestration.",
                envelopeId);
            return;
        }

        if (envelope.Status == ActionExecutionStatus.DeadLettered)
        {
            _logger.LogWarning(
                "Command envelope {EnvelopeId} is dead-lettered. No further processing.",
                envelopeId);
            return;
        }

        // Update status to Processing
        envelope.Status = ActionExecutionStatus.Processing;
        envelope.LastAttemptAt = DateTimeOffset.UtcNow;
        await _commandEnvelopeRepository.UpdateAsync(envelope, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Resolve command runtime type from durable descriptor
        Type? commandType;
        try
        {
            commandType = CommandDescriptor.ResolveCommandType(envelope.CommandTypeFullName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to resolve command type for envelope {EnvelopeId}. Marking dead-lettered.",
                envelopeId);

            envelope.MarkDeadLettered();
            await _commandEnvelopeRepository.UpdateAsync(envelope, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        // Deserialize command payload
        object command;
        try
        {
            command = JsonSerializer.Deserialize(envelope.PayloadJson, commandType)
                      ?? throw new InvalidOperationException("Deserialized command is null.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to deserialize command payload for envelope {EnvelopeId}. Marking dead-lettered.",
                envelopeId);

            envelope.MarkDeadLettered();
            await _commandEnvelopeRepository.UpdateAsync(envelope, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        // Resolve handlers for exact command type only (no polymorphic matching)
        var handlerType = typeof(IBackgroundAction<>).MakeGenericType(commandType);

        // Check if any handlers are registered (without materializing them)
        int handlerCount;
        List<string> handlerTypeNames;
        using (var tempScope = _serviceProvider.CreateScope())
        {
            var handlers = tempScope.ServiceProvider.GetServices(handlerType).ToList();
            handlerCount = handlers.Count;
            handlerTypeNames = handlers
                .Select(h => h?.GetType().FullName ?? "UnknownHandler")
                .ToList();
        }

        if (handlerCount == 0)
        {
            _logger.LogWarning(
                "No handlers registered for command type {CommandType} (envelope {EnvelopeId}). Completing without execution.",
                commandType.FullName,
                envelopeId);

            envelope.MarkCompleted();
            await _commandEnvelopeRepository.UpdateAsync(envelope, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        _logger.LogInformation(
            "Orchestrating {HandlerCount} handler(s) for command type {CommandType} (envelope {EnvelopeId}).",
            handlerCount,
            commandType.FullName,
            envelopeId);

        // Execute handlers in parallel with MaxDegreeOfParallelism=4 and isolated scopes
        var executionTasks = new List<Task<HandlerExecutionResult>>();
        var semaphore = new SemaphoreSlim(MaxDegreeOfParallelism);

        for (int i = 0; i < handlerCount; i++)
        {
            var handlerIndex = i;
            var expectedHandlerTypeName = handlerTypeNames[handlerIndex];
            var task = Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await ExecuteHandlerInIsolatedScopeAsync(
                        handlerType,
                        command,
                        commandType,
                        handlerIndex,
                        expectedHandlerTypeName,
                        cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);
            executionTasks.Add(task);
        }
        var results = await Task.WhenAll(executionTasks);

        // Evaluate results and update envelope status
        var hasFailures = results.Any(r => !r.Success);

        // Record per-handler execution outcomes in durable ExecutionLog
        foreach (var result in results)
        {
            var executionLog = new ActionExecutionLog
            {
                CommandEnvelopeId = envelope.Id,
                ActionType = ActionExecutionLogType.HandlerExecution,
                Status = result.Success ? ActionExecutionStatus.Completed : ActionExecutionStatus.Failed,
                AttemptNumber = envelope.ExecutionLogs.Count(log => log.ActionType == ActionExecutionLogType.Execute),
                HandlerTypeName = result.HandlerTypeName,
                ErrorMessage = result.Success ? null : result.ErrorMessage,
                ErrorDetails = result.Success ? null : result.ErrorDetails
            };
            envelope.ExecutionLogs.Add(executionLog);
        }

        if (hasFailures)
        {
            var errorMessages = results.Where(r => !r.Success).Select(r => r.ErrorMessage).ToList();
            var combinedError = string.Join("; ", errorMessages);

            envelope.RecordAttemptFailure(combinedError);
            await _commandEnvelopeRepository.UpdateAsync(envelope, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (envelope.ShouldRetry())
        {
            _logger.LogWarning(
                "Envelope {EnvelopeId} has failures. Retry attempt {AttemptNumber}/{MaxAttempts}. Hangfire will retry after {NextAttemptAt}.",
                envelopeId,
                envelope.ExecutionLogs.Count(log => log.ActionType == ActionExecutionLogType.Execute),
                CommandEnvelope.MaxRetryAttempts,
                envelope.NextAttemptAt);
            
            // Throw exception to trigger Hangfire AutomaticRetry (60/300/900s delays)
            // This ensures backoff delays are enforced at the job level
            throw new InvalidOperationException(
                $"Envelope {envelopeId} handler execution failed. Retry scheduled at {envelope.NextAttemptAt}. Error: {combinedError}");
        }
            else
            {
                _logger.LogError(
                    "Envelope {EnvelopeId} exceeded max retry attempts. Marking dead-lettered.",
                    envelopeId);

                envelope.MarkDeadLettered();
                await _commandEnvelopeRepository.UpdateAsync(envelope, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            // All handlers succeeded
            envelope.MarkCompleted();
            await _commandEnvelopeRepository.UpdateAsync(envelope, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Envelope {EnvelopeId} completed successfully.",
                envelopeId);
        }
    }

    /// <summary>
    /// Executes a single handler in an isolated scope.
    /// Resolves handler instance from scope to ensure isolated scoped dependencies.
    /// Returns execution result with success flag and error details.
    /// </summary>
    private async Task<HandlerExecutionResult> ExecuteHandlerInIsolatedScopeAsync(
        Type handlerType,
        object command,
        Type commandType,
        int handlerIndex,
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
            if (handlerIndex >= handlers.Count)
            {
                throw new InvalidOperationException(
                    $"Handler index {handlerIndex} out of range (count: {handlers.Count}) for expected handler {expectedHandlerTypeName}.");
            }

            var handler = handlers[handlerIndex]
                ?? throw new InvalidOperationException($"Resolved handler at index {handlerIndex} is null.");
            resolvedHandlerTypeName = handler.GetType().FullName ?? expectedHandlerTypeName;

            // Invoke ExecuteAsync via reflection (handler is dynamic type)
            var executeMethod = handler.GetType().GetMethod("ExecuteAsync");
            if (executeMethod == null)
            {
                throw new InvalidOperationException($"Handler {handler.GetType().FullName} does not have ExecuteAsync method.");
            }

            var task = (Task?)executeMethod.Invoke(handler, new[] { command, cancellationToken });
            if (task != null)
            {
                await task;
            }

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
            var inner = ex is TargetInvocationException { InnerException: not null }
                ? ex.InnerException
                : ex;

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

    private sealed class HandlerExecutionResult
    {
        public bool Success { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;
        public string HandlerTypeName { get; init; } = string.Empty;
        public string? ErrorDetails { get; init; }
    }
}
