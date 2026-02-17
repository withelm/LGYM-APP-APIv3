using LgymApi.Application.Repositories;

namespace LgymApi.Infrastructure.UnitOfWork;

internal sealed class NoOpUnitOfWorkTransaction : IUnitOfWorkTransaction
{
    public static NoOpUnitOfWorkTransaction Instance { get; } = new();

    private NoOpUnitOfWorkTransaction()
    {
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
