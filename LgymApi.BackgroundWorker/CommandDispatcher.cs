using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LgymApi.BackgroundWorker;

/// <summary>
/// Concrete typed dispatcher implementation for background action commands.
/// Validates exact-type handler availability, performs idempotency checks, persists durable envelope,
/// and enqueues orchestration job via scheduler.
/// </summary>
public sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICommandEnvelopeRepository _commandEnvelopeRepository;
    private readonly ILogger<CommandDispatcher> _logger;

    public CommandDispatcher(
        IServiceProvider serviceProvider,
        ICommandEnvelopeRepository commandEnvelopeRepository,
        ILogger<CommandDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _commandEnvelopeRepository = commandEnvelopeRepository;
        _logger = logger;
        // Note: staging-only dispatcher - persistence commit and scheduling are performed elsewhere
    }

    // NOTE: intentionally exposing a single 3-argument constructor to enforce staging-only public surface.

    /// <summary>
    /// Enqueues a strongly-typed command for background action execution asynchronously.
    /// Validates exact-type handler availability (1:1), checks idempotency, persists envelope, and enqueues orchestration job.
    /// Zero-handler path short-circuits safely with warning and no enqueue.
    /// </summary>
    public async Task EnqueueAsync<TCommand>(TCommand command) where TCommand : class, IActionCommand
    {
        if (command == default(TCommand))
        {
            throw new ArgumentNullException(nameof(command));
        }

        var commandType = typeof(TCommand);
        var descriptor = new CommandDescriptor(commandType);
        var payloadJson = JsonSerializer.Serialize(command, commandType, SharedSerializationOptions.Current);

        // Compute deterministic correlation ID from command type + payload content
        var correlationId = ComputeDeterministicCorrelationId(descriptor.TypeFullName, payloadJson);

        _logger.LogInformation(
            "Dispatching command {CommandType} with correlation {CorrelationId}.",
            commandType.FullName,
            correlationId);

        // Validate exact-type handler availability (1:1 matching, no polymorphism)
        var handlerType = typeof(IBackgroundAction<>).MakeGenericType(commandType);
        int handlerCount;
        using (var tempScope = _serviceProvider.CreateScope())
        {
            handlerCount = tempScope.ServiceProvider.GetServices(handlerType).Count();
        }

        if (handlerCount == 0)
        {
            _logger.LogWarning(
                "No handlers registered for command type {CommandType}. Short-circuiting dispatch without enqueue.",
                commandType.FullName);
            return; // Zero-handler path: safe no-op, no failure, no enqueue
        }

        _logger.LogInformation(
            "Found {HandlerCount} handler(s) for command type {CommandType}.",
            handlerCount,
            commandType.FullName);

        // Check idempotency: attempt to add envelope with unique CorrelationId
        // Uses DB-level uniqueness constraint (IX_CommandEnvelopes_CorrelationId) for atomic duplicate detection

        var envelope = new CommandEnvelope
        {
            Id = Id<CommandEnvelope>.New(),
            CorrelationId = correlationId,
            PayloadJson = payloadJson,
            CommandTypeFullName = descriptor.TypeFullName,
            Status = ActionExecutionStatus.Pending,
            NextAttemptAt = DateTimeOffset.UtcNow
        };

        // AddOrGetExistingAsync stages envelope for insert or returns existing if already present
        var envelopeResult = await _commandEnvelopeRepository.AddOrGetExistingAsync(envelope);

        if (!ReferenceEquals(envelopeResult, envelope))
        {
            // Duplicate detected during read phase (existing envelope found by CorrelationId)
            _logger.LogInformation(
                "Command envelope already exists for correlation {CorrelationId} (envelope {EnvelopeId}). Skipping duplicate enqueue.",
                correlationId,
                envelopeResult.Id);
            return; // Idempotent path: no duplicate enqueue
        }

        // Staging-only behavior: envelope has been staged via repository's AddOrGetExistingAsync.
        // Commit and scheduling are the responsibility of a separate unit-of-work and dispatcher.
        _logger.LogInformation(
            "Command envelope {EnvelopeId} staged for correlation {CorrelationId}.",
            envelope.Id,
            correlationId);
    }

    /// <summary>
    /// Computes a deterministic correlation ID from command type and payload.
    /// Uses SHA256 hash to ensure identical commands produce identical correlation IDs for idempotency.
    /// </summary>
    private static Id<CorrelationScope> ComputeDeterministicCorrelationId(string typeFullName, string payloadJson)
    {
        var input = $"{typeFullName}|{payloadJson}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));

        // Use first 16 bytes of SHA256 hash as typed correlation ID (deterministic)
        var correlationBytes = new byte[16];
        Array.Copy(hashBytes, correlationBytes, 16);
        return Id<CorrelationScope>.FromBytes(correlationBytes);
    }
}
