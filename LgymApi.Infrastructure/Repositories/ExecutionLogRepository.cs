using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class ExecutionLogRepository : IExecutionLogRepository
{
    private readonly AppDbContext _dbContext;

    public ExecutionLogRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(ExecutionLog log, CancellationToken cancellationToken = default)
    {
        await _dbContext.ExecutionLogs.AddAsync(log, cancellationToken);
    }

    public async Task<List<ExecutionLog>> GetByCommandEnvelopeIdAsync(Guid commandEnvelopeId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ExecutionLogs
            .Where(x => x.CommandEnvelopeId == commandEnvelopeId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
