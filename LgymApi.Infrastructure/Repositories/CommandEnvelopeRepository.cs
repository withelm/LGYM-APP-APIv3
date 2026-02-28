using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class CommandEnvelopeRepository : ICommandEnvelopeRepository
{
    private readonly AppDbContext _dbContext;

    public CommandEnvelopeRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(CommandEnvelope envelope, CancellationToken cancellationToken = default)
    {
        await _dbContext.CommandEnvelopes.AddAsync(envelope, cancellationToken);
    }

    public Task<CommandEnvelope?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.CommandEnvelopes
            .Include(e => e.ExecutionLogs)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<CommandEnvelope?> FindByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        return _dbContext.CommandEnvelopes
            .Include(e => e.ExecutionLogs)
            .FirstOrDefaultAsync(x => x.CorrelationId == correlationId, cancellationToken);
    }

    public async Task<List<CommandEnvelope>> GetPendingRetriesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _dbContext.CommandEnvelopes
            .Include(e => e.ExecutionLogs)
            .Where(x => x.Status == ActionExecutionStatus.Failed
                        && x.NextAttemptAt != null
                        && x.NextAttemptAt <= now)
            .ToListAsync(cancellationToken);
    }

    public Task UpdateAsync(CommandEnvelope envelope, CancellationToken cancellationToken = default)
    {
        _dbContext.CommandEnvelopes.Update(envelope);
        return Task.CompletedTask;
    }

    public async Task<CommandEnvelope> AddOrGetExistingAsync(CommandEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var existing = await FindByCorrelationIdAsync(envelope.CorrelationId, cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        await AddAsync(envelope, cancellationToken);
        return envelope;
    }
}
