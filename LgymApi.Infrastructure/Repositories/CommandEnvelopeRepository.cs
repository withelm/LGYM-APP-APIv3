using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
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

    public Task<CommandEnvelope?> FindByIdAsync(Id<CommandEnvelope> id, CancellationToken cancellationToken = default)
    {
        return _dbContext.CommandEnvelopes
            .Include(e => e.ExecutionLogs)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<CommandEnvelope?> FindByCorrelationIdAsync(Id<CorrelationScope> correlationId, CancellationToken cancellationToken = default)
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

    /// <summary>
    /// Adds a command envelope or returns the existing one if duplicate.
    /// 
    /// Implementation strategy:
    /// 1. Check for existing envelope by CorrelationId (read phase)
    /// 2. If exists, return it immediately (idempotent path)
    /// 3. If not exists, stage new envelope for insert (write phase)
    /// 
    /// Duplicate protection relies on DB unique constraint (IX_CommandEnvelopes_CorrelationId).
    /// Concurrent duplicate attempts will be rejected at SaveChangesAsync (caller responsibility).
    /// Caller must handle DbUpdateException and retry by calling this method again to fetch existing.
    /// 
    /// This is a "stage-then-persist" pattern aligned with Unit of Work discipline:
    /// - Repository stages changes but does not call SaveChanges
    /// - Caller controls transaction boundary and handles constraint violations
    /// - On constraint violation, caller detaches failed entity and retries this method
    /// </summary>
    public async Task<CommandEnvelope> AddOrGetExistingAsync(CommandEnvelope envelope, CancellationToken cancellationToken = default)
    {
        // Read phase: check for existing envelope by unique CorrelationId
        var existing = await FindByCorrelationIdAsync(envelope.CorrelationId, cancellationToken);
        if (existing != null)
        {
            return existing; // Idempotent: duplicate detected, return existing
        }

        // Write phase: stage new envelope for insert (caller will persist via SaveChangesAsync)
        await AddAsync(envelope, cancellationToken);
        return envelope;
    }
}
