using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface IActionExecutionLogRepository
{
    Task AddAsync(ActionExecutionLog log, CancellationToken cancellationToken = default);

    Task<List<ActionExecutionLog>> GetByCommandEnvelopeIdAsync(Guid commandEnvelopeId, CancellationToken cancellationToken = default);
}
