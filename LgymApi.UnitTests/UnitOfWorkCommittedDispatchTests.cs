using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UnitOfWorkCommittedDispatchTests
{
    [Test]
    public async Task SaveChangesAsync_DispatchesCommittedIntents_AfterPersistenceSucceeds()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"uow-dispatch-{Id<UnitOfWorkCommittedDispatchTests>.New()}")
            .Options;

        await using var dbContext = new AppDbContext(options);
        var dispatcher = new RecordingCommittedIntentDispatcher();
        var unitOfWork = new EfUnitOfWork(dbContext, dispatcher, NullLogger<EfUnitOfWork>.Instance);

        dbContext.CommandEnvelopes.Add(new CommandEnvelope
        {
            Id = Id<CommandEnvelope>.New(),
            CorrelationId = Id<CorrelationScope>.New(),
            CommandTypeFullName = "Test.Command",
            PayloadJson = "{}",
            Status = ActionExecutionStatus.Pending
        });

        await unitOfWork.SaveChangesAsync();

        Assert.That(dispatcher.CallCount, Is.EqualTo(1));
    }

    [Test]
    public async Task CommitAsync_WhenDispatchFails_DoesNotThrowAndKeepsRecoveryPath()
    {
        var dbTransaction = new FakeDbContextTransaction();
        var dispatcher = new ThrowingCommittedIntentDispatcher();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"uow-commit-dispatch-{Id<UnitOfWorkCommittedDispatchTests>.New()}")
            .Options;
        await using var dbContext = new AppDbContext(options);
        var transaction = new EfUnitOfWorkTransaction(dbTransaction, dbContext, dispatcher, NullLogger<EfUnitOfWork>.Instance);

        Assert.DoesNotThrowAsync(async () => await transaction.CommitAsync());
        Assert.Multiple(() =>
        {
            Assert.That(dbTransaction.CommitCalls, Is.EqualTo(1));
            Assert.That(dispatcher.CallCount, Is.EqualTo(1));
        });
    }

    private sealed class RecordingCommittedIntentDispatcher : ICommittedIntentDispatcher
    {
        public int CallCount { get; private set; }

        public Task DispatchCommittedIntentsAsync(CancellationToken cancellationToken = default)
        {
            CallCount += 1;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingCommittedIntentDispatcher : ICommittedIntentDispatcher
    {
        public int CallCount { get; private set; }

        public Task DispatchCommittedIntentsAsync(CancellationToken cancellationToken = default)
        {
            CallCount += 1;
            throw new InvalidOperationException("Simulated scheduler outage after commit");
        }
    }

    private sealed class FakeDbContextTransaction : IDbContextTransaction
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
        public int CommitCalls { get; private set; }
        public bool SupportsSavepoints => false;

        public void Commit()
        {
            CommitCalls += 1;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCalls += 1;
            return Task.CompletedTask;
        }

        public void Rollback()
        {
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void CreateSavepoint(string name)
        {
            throw new NotSupportedException();
        }

        public Task CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public void RollbackToSavepoint(string name)
        {
            throw new NotSupportedException();
        }

        public Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public void ReleaseSavepoint(string name)
        {
            throw new NotSupportedException();
        }

        public Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
