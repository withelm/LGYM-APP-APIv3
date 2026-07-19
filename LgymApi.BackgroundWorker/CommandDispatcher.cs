using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Runtime;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ApplicationInvitationAcceptedCommand = LgymApi.Application.Coaching.Contracts.BackgroundCommands.InvitationAcceptedCommand;

namespace LgymApi.BackgroundWorker;

/// <summary>
/// Concrete typed command dispatcher.
/// Validates exact-type handler availability, performs idempotency checks, and persists a durable envelope.
/// </summary>
public sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly IBackgroundActionResolver _backgroundActionResolver;
    private readonly CommandContractRegistry _commandContractRegistry;
    private readonly ICommandEnvelopeRepository _commandEnvelopeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CommandDispatcher> _logger;

    public CommandDispatcher(
        IBackgroundActionResolver backgroundActionResolver,
        CommandContractRegistry commandContractRegistry,
        ICommandEnvelopeRepository commandEnvelopeRepository,
        IUnitOfWork unitOfWork,
        ILogger<CommandDispatcher> logger)
    {
        _backgroundActionResolver = backgroundActionResolver;
        _commandContractRegistry = commandContractRegistry;
        _commandEnvelopeRepository = commandEnvelopeRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Persists a strongly-typed command for background action execution asynchronously.
    /// Validates exact-type handler availability (1:1), checks idempotency, and persists an envelope.
    /// Zero-handler path short-circuits safely with warning and no persistence.
    /// </summary>
    public async Task EnqueueAsync<TCommand>(TCommand command) where TCommand : class, IActionCommand
    {
        if (command == default(TCommand))
        {
            throw new ArgumentNullException(nameof(command));
        }

        var commandType = typeof(TCommand);

        // Validate exact-type handler availability (1:1 matching, no polymorphism)
        var handlerCount = _backgroundActionResolver.GetHandlerTypeNames(commandType).Count;

        if (handlerCount == 0)
        {
            _logger.LogWarning(
                "No handlers registered for command. Skipping durable envelope persistence.");
            return; // Zero-handler path: safe no-op, no failure, no persistence
        }

        var descriptor = _commandContractRegistry.DescribeForDispatch(commandType);
        var payloadJson = descriptor.CanonicalId == CommandContractRegistry.InvitationAcceptedCanonicalId
            ? SerializeInvitationAcceptedCommand(command)
            : JsonSerializer.Serialize(command, commandType, SharedSerializationOptions.Current);
        var correlationId = ComputeDeterministicCorrelationId(descriptor.CanonicalId, payloadJson);

        _logger.LogInformation(
                "Persisting command {CommandId} with correlation {CorrelationId}.",
            descriptor.CanonicalId,
            correlationId);

        _logger.LogInformation(
            "Found {HandlerCount} handler(s) for command {CommandId}.",
            handlerCount,
            descriptor.CanonicalId);

        // Check idempotency: attempt to add envelope with unique CorrelationId
        // Uses DB-level uniqueness constraint (IX_CommandEnvelopes_CorrelationId) for atomic duplicate detection

        var envelope = new CommandEnvelope
        {
            Id = Id<CommandEnvelope>.New(),
            CorrelationId = correlationId,
            PayloadJson = payloadJson,
            CommandTypeFullName = descriptor.CanonicalId,
            Status = ActionExecutionStatus.Pending,
            NextAttemptAt = DateTimeOffset.UtcNow
        };

        // AddOrGetExistingAsync records the envelope or returns an existing one.
        var envelopeResult = await _commandEnvelopeRepository.AddOrGetExistingAsync(envelope);

        if (!ReferenceEquals(envelopeResult, envelope))
        {
            // Duplicate detected during read phase (existing envelope found by CorrelationId)
            _logger.LogInformation(
                "Command envelope already exists for correlation {CorrelationId} (envelope {EnvelopeId}). Skipping duplicate persistence.",
                correlationId,
                envelopeResult.Id);
            return; // Idempotent path: no duplicate persistence
        }

        // Persist new envelope - DB unique constraint will enforce duplicate protection atomically
        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsExactDuplicateEnvelopeViolation(ex))
        {
            // Conflict: unique constraint violation on CorrelationId (concurrent duplicate insert)
            // This handles the race condition where two concurrent callers passed the read phase
            // but both attempted insert - DB constraint ensures only one succeeds
            
            _logger.LogInformation(
                ex,
                "Unique constraint violation on CorrelationId {CorrelationId}. Concurrent duplicate detected. Fetching existing envelope.",
                correlationId);

            // Detach the failed envelope to avoid tracking conflicts
            _commandEnvelopeRepository.Detach(envelope);

            // Fetch the winning envelope that was persisted by concurrent caller
            var existing = await _commandEnvelopeRepository.FindByCorrelationIdAsync(correlationId);
            
            if (existing == null)
            {
                // Edge case: constraint violation but envelope not found
                // This indicates soft-delete race or unexpected DB state
                _logger.LogError(
                    "Unique constraint violation but existing envelope not found for correlation {CorrelationId}. Re-throwing exception.",
                    correlationId);
                throw;
            }

            _logger.LogInformation(
                "Concurrent duplicate resolved: using existing envelope {EnvelopeId} for correlation {CorrelationId}. Skipping persistence.",
                existing.Id,
                correlationId);
            
            return; // Idempotent path: conflict resolved, skip persistence
        }

        _logger.LogInformation(
            "Command envelope {EnvelopeId} persisted for correlation {CorrelationId}.",
            envelope.Id,
            correlationId);
    }

    /// <summary>
    /// Computes a deterministic correlation ID from the canonical command ID and payload.
    /// Uses SHA256 hash to ensure identical commands produce identical correlation IDs for idempotency.
    /// </summary>
    private static Id<CorrelationScope> ComputeDeterministicCorrelationId(string canonicalCommandId, string payloadJson)
    {
        var input = $"{canonicalCommandId}|{payloadJson}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));

        // Use first 16 bytes of SHA256 hash as typed correlation ID (deterministic)
        var correlationBytes = new byte[16];
        Array.Copy(hashBytes, correlationBytes, 16);
        return Id<CorrelationScope>.FromBytes(correlationBytes);
    }

    /// <summary>
    /// Serializes InvitationAcceptedCommand manually to preserve its fixed durable bytes.
    /// </summary>
    private static string SerializeInvitationAcceptedCommand(object command)
    {
        var invitationId = command switch
        {
            ApplicationInvitationAcceptedCommand applicationCommand => applicationCommand.InvitationId,
            _ => throw new InvalidOperationException(
                $"Command '{command.GetType()}' is registered with the InvitationAccepted canonical ID but has incompatible metadata.")
        };

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("invitationId");
            writer.WriteStringValue(invitationId.ToString());
            writer.WriteEndObject();
            writer.Flush();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static bool IsExactDuplicateEnvelopeViolation(DbUpdateException exception)
    {
        const string commandEnvelopeCorrelationIndex = "IX_CommandEnvelopes_CorrelationId";

        return exception.InnerException is PostgresException postgresException
            && string.Equals(postgresException.SqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal)
            && string.Equals(postgresException.ConstraintName, commandEnvelopeCorrelationIndex, StringComparison.Ordinal);
    }
}
