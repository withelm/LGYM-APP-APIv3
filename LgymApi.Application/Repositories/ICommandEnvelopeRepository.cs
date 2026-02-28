using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.Application.Repositories;

/// <summary>
/// Repository for managing CommandEnvelope entities with idempotency and retry orchestration queries.
/// </summary>
public interface ICommandEnvelopeRepository
{
    /// <summary>
    /// Adds a new command envelope to the repository.
    /// </summary>
    Task AddAsync(CommandEnvelope envelope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a command envelope by its unique identifier.
    /// </summary>
    Task<CommandEnvelope?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a command envelope by correlation ID for idempotency checking.
    /// Used to prevent duplicate command execution for the same logical operation.
    /// </summary>
    Task<CommandEnvelope?> FindByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all command envelopes with status Failed that have NextAttemptAt in the past and should be retried.
    /// Used by the orchestrator to poll for pending retry candidates.
    /// </summary>
    Task<List<CommandEnvelope>> GetPendingRetriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing command envelope. Used after status transitions or attempt recording.
    /// </summary>
    Task UpdateAsync(CommandEnvelope envelope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to add a command envelope atomically, preventing duplicate correlation IDs.
    /// If an envelope with the same correlation ID already exists, returns the existing envelope.
    /// If the new envelope is successfully added, returns the newly created envelope.
    /// Uses database-level uniqueness constraint (or conflict detection) to ensure durable idempotency.
    /// </summary>
    /// <param name="envelope">The command envelope to add (must have unique CorrelationId)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The newly created envelope if added, or the existing envelope if duplicate CorrelationId found</returns>
    Task<CommandEnvelope> AddOrGetExistingAsync(CommandEnvelope envelope, CancellationToken cancellationToken = default);
}
