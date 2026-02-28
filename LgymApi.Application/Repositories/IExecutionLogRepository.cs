using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

/// <summary>
/// Repository for managing ExecutionLog entities - per-action execution audit trail.
/// </summary>
public interface IExecutionLogRepository
{
    /// <summary>
    /// Adds a new execution log entry to the repository.
    /// </summary>
    Task AddAsync(ExecutionLog log, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all execution logs for a specific command envelope, ordered by creation timestamp.
    /// </summary>
    Task<List<ExecutionLog>> GetByCommandEnvelopeIdAsync(Guid commandEnvelopeId, CancellationToken cancellationToken = default);
}
