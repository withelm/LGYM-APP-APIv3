using LgymApi.Application.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.UnitOfWork;

internal sealed class NoOpUnitOfWorkTransaction : IUnitOfWorkTransaction
{
    private readonly DbContext _dbContext;

    public NoOpUnitOfWorkTransaction(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        _dbContext.ChangeTracker.Clear();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
