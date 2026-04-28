using FluentAssertions;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using LgymApi.Infrastructure.Services;
using LgymApi.Infrastructure.UnitOfWork;
using LgymApi.TestUtils.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class CommandDispatcherBoundaryTests
{
    [Test]
    public async Task Pre_Commit_Failure_Does_Not_Enqueue()
    {
        using var harness = CreateHarness(registerHandler: true);
        await harness.DbContext.Database.EnsureCreatedAsync();

        var dispatcher = harness.CreateDispatcher();
        var unitOfWork = new FakeUnitOfWork
        {
            SaveChangesException = new InvalidOperationException("Simulated pre-commit failure")
        };

        await dispatcher.EnqueueAsync(new TestCommand { Value = "pre-commit" });

        var action = async () => await unitOfWork.SaveChangesAsync();
        await action.Should().ThrowAsync<InvalidOperationException>();

        harness.ActionScheduler.DidNotReceive().Enqueue(Arg.Any<Id<CommandEnvelope>>());

        await using var verificationContext = harness.CreateDbContext();
        (await verificationContext.CommandEnvelopes.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Post_Commit_Success_Enqueues_Via_Outbox()
    {
        using var harness = CreateHarness(registerHandler: true);
        await harness.DbContext.Database.EnsureCreatedAsync();

        var dispatcher = harness.CreateDispatcher();
        var unitOfWork = harness.CreateUnitOfWork();

        await dispatcher.EnqueueAsync(new TestCommand { Value = "post-commit" });

        var stagedEnvelope = harness.DbContext.ChangeTracker
            .Entries<CommandEnvelope>()
            .Single()
            .Entity;

        harness.ActionScheduler.DidNotReceive().Enqueue(Arg.Any<Id<CommandEnvelope>>());

        await unitOfWork.SaveChangesAsync();

        harness.ActionScheduler.Received(1).Enqueue(stagedEnvelope.Id);

        await using var verificationContext = harness.CreateDbContext();
        var persistedEnvelope = await verificationContext.CommandEnvelopes.SingleAsync();
        persistedEnvelope.DispatchedAt.Should().NotBeNull();
        persistedEnvelope.SchedulerJobId.Should().Be("job-123");
        persistedEnvelope.Status.Should().Be(ActionExecutionStatus.Pending);
    }

    [Test]
    public async Task Explicit_Transaction_Enqueues_After_CommitAsync_Only()
    {
        await using var sqliteConnection = new SqliteConnection("Data Source=:memory:");
        await sqliteConnection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(sqliteConnection));
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler>();

        using var provider = services.BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var dispatcher = new CommandDispatcher(
            provider,
            new CommandEnvelopeRepository(dbContext),
            NullLogger<CommandDispatcher>.Instance);

        var actionScheduler = Substitute.For<IActionMessageScheduler>();
        actionScheduler.Enqueue(Arg.Any<Id<CommandEnvelope>>()).Returns("job-123");

        var committedIntentDispatcher = Substitute.For<ICommittedIntentDispatcher>();

        var unitOfWork = new EfUnitOfWork(
            dbContext,
            committedIntentDispatcher,
            NullLogger<EfUnitOfWork>.Instance);

        await using var transaction = await unitOfWork.BeginTransactionAsync();
        transaction.Should().BeOfType<EfUnitOfWorkTransaction>();

        await dispatcher.EnqueueAsync(new TestCommand { Value = "commit-only" });
        var stagedEnvelope = dbContext.ChangeTracker.Entries<CommandEnvelope>().Single().Entity;

        committedIntentDispatcher
            .DispatchCommittedIntentsAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                actionScheduler.Enqueue(stagedEnvelope.Id);
                return Task.CompletedTask;
            });

        await unitOfWork.SaveChangesAsync();

        // SaveChanges inside an explicit transaction must not dispatch or enqueue.
        actionScheduler.DidNotReceive().Enqueue(Arg.Any<Id<CommandEnvelope>>());
        await committedIntentDispatcher.DidNotReceive().DispatchCommittedIntentsAsync(Arg.Any<CancellationToken>());

        await transaction.CommitAsync();

        // CommitAsync must dispatch and enqueue.
        await committedIntentDispatcher.Received(1).DispatchCommittedIntentsAsync(Arg.Any<CancellationToken>());
        actionScheduler.Received(1).Enqueue(stagedEnvelope.Id);
    }
    [Test]
    public async Task Zero_Handler_Short_Circuits()
    {
        using var harness = CreateHarness(registerHandler: false);
        await harness.DbContext.Database.EnsureCreatedAsync();

        var dispatcher = harness.CreateDispatcher();

        await dispatcher.EnqueueAsync(new TestCommand { Value = "no-handler" });

        harness.ActionScheduler.DidNotReceive().Enqueue(Arg.Any<Id<CommandEnvelope>>());
        harness.DbContext.ChangeTracker.Entries<CommandEnvelope>().Should().BeEmpty();

        await using var verificationContext = harness.CreateDbContext();
        (await verificationContext.CommandEnvelopes.CountAsync()).Should().Be(0);
    }

    private static Harness CreateHarness(bool registerHandler)
    {
        var databaseName = $"dispatcher-boundary-{Guid.NewGuid():N}";
        var databaseRoot = new InMemoryDatabaseRoot();
        var actionScheduler = Substitute.For<IActionMessageScheduler>();
        actionScheduler.Enqueue(Arg.Any<Id<CommandEnvelope>>()).Returns("job-123");

        var emailScheduler = Substitute.For<IEmailBackgroundScheduler>();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));
        services.AddSingleton<IActionMessageScheduler>(actionScheduler);
        services.AddSingleton<IEmailBackgroundScheduler>(emailScheduler);
        if (registerHandler)
        {
            services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler>();
        }

        var builtProvider = services.BuildServiceProvider();
        var scope = builtProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return new Harness(builtProvider, builtProvider, scope, dbContext, actionScheduler, databaseName, databaseRoot);
    }

    private sealed class Harness(
        IDisposable disposableProvider,
        IServiceProvider serviceProvider,
        IServiceScope scope,
        AppDbContext dbContext,
        IActionMessageScheduler actionScheduler,
        string databaseName,
        InMemoryDatabaseRoot databaseRoot) : IDisposable
    {
        public AppDbContext DbContext { get; } = dbContext;
        public IActionMessageScheduler ActionScheduler { get; } = actionScheduler;

        public CommandDispatcher CreateDispatcher()
            => new(
                serviceProvider,
                new CommandEnvelopeRepository(DbContext),
                NullLogger<CommandDispatcher>.Instance);

        public CommittedIntentDispatcher CreateCommittedIntentDispatcher()
            => new(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<CommittedIntentDispatcher>.Instance);

        public EfUnitOfWork CreateUnitOfWork()
            => new(
                DbContext,
                CreateCommittedIntentDispatcher(),
                NullLogger<EfUnitOfWork>.Instance);

        public AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName, databaseRoot)
                .Options;

            return new AppDbContext(options);
        }

        public void Dispose()
        {
            DbContext.Dispose();
            scope.Dispose();
            disposableProvider.Dispose();
        }
    }

    private sealed class TestCommand : IActionCommand
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class TestActionHandler : IBackgroundAction<TestCommand>
    {
        public Task ExecuteAsync(TestCommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeDbContextTransaction : IDbContextTransaction
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
        public bool SupportsSavepoints => false;

        public void Commit()
        {
        }

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Rollback()
        {
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void CreateSavepoint(string name) => throw new NotSupportedException();
        public Task CreateSavepointAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void RollbackToSavepoint(string name) => throw new NotSupportedException();
        public Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void ReleaseSavepoint(string name) => throw new NotSupportedException();
        public Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}







