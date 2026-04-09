using LgymApi.Application.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace LgymApi.Infrastructure.UnitOfWork;

public sealed class EfUnitOfWorkTransaction : IUnitOfWorkTransaction
{
    private readonly IDbContextTransaction _transaction;
    private readonly DbContext _dbContext;
    private readonly ICommittedIntentDispatcher? _committedIntentDispatcher;
    private readonly ILogger<EfUnitOfWork>? _logger;

    public EfUnitOfWorkTransaction(
        IDbContextTransaction transaction,
        DbContext dbContext,
        ICommittedIntentDispatcher? committedIntentDispatcher = null,
        ILogger<EfUnitOfWork>? logger = null)
    {
        _transaction = transaction;
        _dbContext = dbContext;
        _committedIntentDispatcher = committedIntentDispatcher;
        _logger = logger;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.CommitAsync(cancellationToken);

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
                "Committed-intent dispatch failed after transaction commit. Intents remain recoverable by recovery flow.");
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.RollbackAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
    }

    public ValueTask DisposeAsync()
    {
        return _transaction.DisposeAsync();
    }
}
