using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;

namespace LgymApi.Infrastructure.UnitOfWork;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private const string CommandEnvelopeCorrelationConstraintName = "IX_CommandEnvelopes_CorrelationId";

    private readonly AppDbContext _dbContext;
    private readonly ICommittedIntentDispatcher? _committedIntentDispatcher;
    private readonly ILogger<EfUnitOfWork>? _logger;

    public EfUnitOfWork(
        AppDbContext dbContext,
        ICommittedIntentDispatcher? committedIntentDispatcher = null,
        ILogger<EfUnitOfWork>? logger = null)
    {
        _dbContext = dbContext;
        _committedIntentDispatcher = committedIntentDispatcher;
        _logger = logger;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var saved = await _dbContext.SaveChangesAsync(cancellationToken);

            if (_dbContext.Database.CurrentTransaction == null)
            {
                await TryDispatchCommittedIntentsAsync(cancellationToken);
            }

            return saved;
        }
        catch (DbUpdateException exception)
        {
            if (!await TryDetachDuplicateCommandEnvelopeEntriesAsync(exception, cancellationToken))
            {
                throw;
            }

            var retried = await _dbContext.SaveChangesAsync(cancellationToken);

            if (_dbContext.Database.CurrentTransaction == null)
            {
                await TryDispatchCommittedIntentsAsync(cancellationToken);
            }

            return retried;
        }
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new NoOpUnitOfWorkTransaction(_dbContext);
        }

        var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new EfUnitOfWorkTransaction(transaction, _dbContext, _committedIntentDispatcher, _logger);
    }

    public void DetachEntity<TEntity>(TEntity entity) where TEntity : class
    {
        _dbContext.Entry(entity).State = EntityState.Detached;
    }

    private async Task<bool> TryDetachDuplicateCommandEnvelopeEntriesAsync(DbUpdateException exception, CancellationToken cancellationToken)
    {
        if (!IsCommandEnvelopeCorrelationConflict(exception))
        {
            return false;
        }

        var addedEntries = _dbContext.ChangeTracker.Entries<CommandEnvelope>()
            .Where(entry => entry.State == EntityState.Added)
            .ToList();

        if (addedEntries.Count == 0)
        {
            return false;
        }

        var commandEnvelopeRepository = new CommandEnvelopeRepository(_dbContext);
        var duplicateEntries = new List<EntityEntry<CommandEnvelope>>();
        var seenCorrelationIds = new HashSet<object>();

        foreach (var entry in addedEntries)
        {
            var existing = await commandEnvelopeRepository.FindByCorrelationIdAsync(entry.Entity.CorrelationId, cancellationToken);
            if (existing != null)
            {
                duplicateEntries.Add(entry);
                continue;
            }

            if (!seenCorrelationIds.Add(entry.Entity.CorrelationId))
            {
                duplicateEntries.Add(entry);
            }
        }

        if (duplicateEntries.Count == 0)
        {
            return false;
        }

        foreach (var entry in duplicateEntries)
        {
            entry.State = EntityState.Detached;
        }

        return true;
    }

    private static bool IsCommandEnvelopeCorrelationConflict(DbUpdateException exception)
    {
        return ExceptionContainsMessage(exception, CommandEnvelopeCorrelationConstraintName)
               || ExceptionContainsMessage(exception, "duplicate key value violates unique constraint")
               || ExceptionContainsMessage(exception, "UNIQUE constraint failed");
    }

    private static bool ExceptionContainsMessage(Exception exception, string message)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current.Message.Contains(message, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task TryDispatchCommittedIntentsAsync(CancellationToken cancellationToken)
    {
        if (_committedIntentDispatcher == null)
        {
            return;
        }

        try
        {
            await _committedIntentDispatcher.DispatchCommittedIntentsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(
                ex,
                "Committed-intent dispatch failed after SaveChanges. Intents remain recoverable by recovery flow.");
        }
    }
}
