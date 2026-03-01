using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IActionMessageScheduler _scheduler;
    private readonly ILogger<CommandDispatcher> _logger;

    public CommandDispatcher(
        IServiceProvider serviceProvider,
        ICommandEnvelopeRepository commandEnvelopeRepository,
        IUnitOfWork unitOfWork,
        IActionMessageScheduler scheduler,
        ILogger<CommandDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _commandEnvelopeRepository = commandEnvelopeRepository;
        _unitOfWork = unitOfWork;
        _scheduler = scheduler;
        _logger = logger;
    }

    /// <summary>
    /// Enqueues a strongly-typed command for background action execution.
    /// Validates exact-type handler availability (1:1), checks idempotency, persists envelope, and enqueues orchestration job.
    /// Zero-handler path short-circuits safely with warning and no enqueue.
    /// </summary>
    public void Enqueue<TCommand>(TCommand command) where TCommand : class, IActionCommand
    {
        if (command == default(TCommand))
        {
            throw new ArgumentNullException(nameof(command));
        }

        var commandType = typeof(TCommand);
        var descriptor = new CommandDescriptor(commandType);
        var payloadJson = JsonSerializer.Serialize(command, commandType);

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

        var envelope = new CommandEnvelope
        {
            CorrelationId = correlationId,
            PayloadJson = payloadJson,
            CommandTypeFullName = descriptor.TypeFullName,
            Status = ActionExecutionStatus.Pending,
            NextAttemptAt = DateTimeOffset.UtcNow
        };

        // AddOrGetExistingAsync uses durable idempotency check (database-level uniqueness or conflict detection)
        var envelopeResult = _commandEnvelopeRepository.AddOrGetExistingAsync(envelope).GetAwaiter().GetResult();

        if (!ReferenceEquals(envelopeResult, envelope))
        {
            // Duplicate: existing envelope found with same CorrelationId
            _logger.LogInformation(
                "Command envelope already exists for correlation {CorrelationId} (envelope {EnvelopeId}). Skipping duplicate enqueue.",
                correlationId,
                envelopeResult.Id);
            return; // Idempotent path: no duplicate enqueue
        }

        // Persist new envelope
        _unitOfWork.SaveChangesAsync().GetAwaiter().GetResult();

        _logger.LogInformation(
            "Command envelope {EnvelopeId} persisted for correlation {CorrelationId}.",
            envelope.Id,
            correlationId);

        // Enqueue orchestration job by envelope ID only (durable reference)
        _scheduler.Enqueue(envelope.Id);

        _logger.LogInformation(
            "Command envelope {EnvelopeId} enqueued for orchestration.",
            envelope.Id);
    }

    /// <summary>
    /// Computes a deterministic correlation ID from command type and payload.
    /// Uses SHA256 hash to ensure identical commands produce identical correlation IDs for idempotency.
    /// </summary>
    private static Guid ComputeDeterministicCorrelationId(string typeFullName, string payloadJson)
    {
        var input = $"{typeFullName}|{payloadJson}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));

        // Use first 16 bytes of SHA256 hash as Guid (deterministic)
        var guidBytes = new byte[16];
        Array.Copy(hashBytes, guidBytes, 16);
        return new Guid(guidBytes);
    }
}
