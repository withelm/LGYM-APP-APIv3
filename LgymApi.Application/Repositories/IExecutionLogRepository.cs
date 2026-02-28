using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

/// <summary>
/// Repository for managing ActionExecutionLog entities - per-action execution audit trail.
/// </summary>
public interface IExecutionLogRepository
{
    /// <summary>
    /// Adds a new execution log entry to the repository.
    /// </summary>
    Task AddAsync(ActionExecutionLog log, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all execution logs for a specific command envelope, ordered by creation timestamp.
    /// </summary>
    Task<List<ActionExecutionLog>> GetByCommandEnvelopeIdAsync(Guid commandEnvelopeId, CancellationToken cancellationToken = default);
}
