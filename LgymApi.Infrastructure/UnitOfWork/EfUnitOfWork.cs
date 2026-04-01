using LgymApi.Application.Repositories;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LgymApi.Infrastructure.UnitOfWork;

public sealed class EfUnitOfWork : IUnitOfWork
{
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
        var saved = await _dbContext.SaveChangesAsync(cancellationToken);

        if (_dbContext.Database.CurrentTransaction == null)
        {
            await TryDispatchCommittedIntentsAsync(cancellationToken);
        }

        return saved;
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
        {
            return NoOpUnitOfWorkTransaction.Instance;
        }

        var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new EfUnitOfWorkTransaction(transaction, _committedIntentDispatcher, _logger);
    }

    public void DetachEntity<TEntity>(TEntity entity) where TEntity : class
    {
        _dbContext.Entry(entity).State = EntityState.Detached;
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
