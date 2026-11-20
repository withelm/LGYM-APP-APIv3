using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.UnitOfWork;
using LgymApi.Platform.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ActorRowSecurityScopeTests
{
    [Test]
    public async Task BeginAsync_WhenActorIsEmpty_RejectsBeforeStartingTransaction()
    {
        await using var dbContext = CreateInMemoryContext();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var factory = CreateFactory(dbContext, unitOfWork);

        var action = async () => await factory.BeginAsync(Id<ActorReference>.Empty);

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<ArgumentException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("Actor ID cannot be empty.*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        await unitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task BeginAsync_WhenUsingInMemory_ReturnsTheExistingNoOpTransactionWithoutRelationalState()
    {
        await using var dbContext = CreateInMemoryContext();
        var unitOfWork = new EfUnitOfWork(dbContext);
        var factory = CreateFactory(dbContext, unitOfWork);

        await using var transaction = await factory.BeginAsync(Id<ActorReference>.New());

        Assert.That(transaction.GetType().Name, Is.EqualTo("NoOpUnitOfWorkTransaction"));
        Assert.That(dbContext.Database.CurrentTransaction, Is.Null);
    }

    [Test]
    public async Task BeginAsync_WhenRelationalProviderIsNotNpgsql_DisposesTransactionAndPreservesTracking()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateSqliteContext(connection);
        var unitOfWork = new EfUnitOfWork(dbContext);
        var factory = CreateFactory(dbContext, unitOfWork);
        var envelope = CreateEnvelope();
        dbContext.CommandEnvelopes.Add(envelope);

        var action = async () => await factory.BeginAsync(Id<ActorReference>.New());

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<NotSupportedException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("Actor row-security scopes require the PostgreSQL Npgsql provider.").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        Assert.That(dbContext.Database.CurrentTransaction, Is.Null);
        Assert.That(dbContext.Entry(envelope).State, Is.EqualTo(EntityState.Added));
    }

    [Test]
    public async Task BeginAsync_WhenCancellationIsRequested_DoesNotReturnAScopeOrClearTracking()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateSqliteContext(connection);
        var unitOfWork = new EfUnitOfWork(dbContext);
        var factory = CreateFactory(dbContext, unitOfWork);
        var envelope = CreateEnvelope();
        dbContext.CommandEnvelopes.Add(envelope);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = async () => await factory.BeginAsync(Id<ActorReference>.New(), cancellationSource.Token);

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<OperationCanceledException>());
        Assert.That(dbContext.Database.CurrentTransaction, Is.Null);
        Assert.That(dbContext.Entry(envelope).State, Is.EqualTo(EntityState.Added));
    }

    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"actor-row-security-{Id<ActorRowSecurityScopeTests>.New()}")
            .Options;

        return new AppDbContext(options);
    }

    private static AppDbContext CreateSqliteContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }

    private static IActorRowSecurityScopeFactory CreateFactory(AppDbContext dbContext, IUnitOfWork unitOfWork)
    {
        var factoryType = typeof(EfUnitOfWork).Assembly.GetType(
            "LgymApi.Infrastructure.RowSecurity.EfActorRowSecurityScopeFactory",
            throwOnError: true)!;

        return (IActorRowSecurityScopeFactory)Activator.CreateInstance(factoryType, dbContext, unitOfWork)!;
    }

    private static CommandEnvelope CreateEnvelope() => new()
    {
        Id = Id<CommandEnvelope>.New(),
        CorrelationId = Id<CorrelationScope>.New(),
        CommandTypeFullName = "ActorScope.Test",
        PayloadJson = "{}",
        Status = ActionExecutionStatus.Pending
    };
}
