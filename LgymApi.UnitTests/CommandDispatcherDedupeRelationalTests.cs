using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.UnitOfWork;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.UnitTests;

// SQLite is intentional here: unlike EF InMemory, it enforces the real unique index on
// CommandEnvelope.CorrelationId and raises a relational DbUpdateException path.
[TestFixture]
public sealed class CommandDispatcherDedupeRelationalTests
{
    [Test]
    public async Task Concurrent_Duplicate_Is_Deduped_At_Commit_With_Real_Constraint()
    {
        await using var sqliteConnection = new SqliteConnection("Data Source=:memory:");
        await sqliteConnection.OpenAsync();

        var options = CreateOptions(sqliteConnection);

        await using (var setupContext = new AppDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        var correlationId = Id<CorrelationScope>.New();

        await using (var contextA = new AppDbContext(options))
        {
            contextA.CommandEnvelopes.Add(CreateEnvelope(correlationId, "first"));
            var unitOfWorkA = new EfUnitOfWork(contextA);
            await unitOfWorkA.SaveChangesAsync();
        }

        var duplicateEnvelope = CreateEnvelope(correlationId, "second");

        await using (var contextB = new AppDbContext(options))
        {
            contextB.CommandEnvelopes.Add(duplicateEnvelope);
            var unitOfWorkB = new EfUnitOfWork(contextB);

            await unitOfWorkB.SaveChangesAsync();

            contextB.Entry(duplicateEnvelope).State.Should().Be(EntityState.Detached);
        }

        await using var verificationContext = new AppDbContext(options);
        var persisted = await verificationContext.CommandEnvelopes
            .Where(x => x.CorrelationId == correlationId)
            .ToListAsync();

        persisted.Should().HaveCount(1);
    }

    [Test]
    public async Task Domain_Changes_Survive_Envelope_Dedupe()
    {
        await using var sqliteConnection = new SqliteConnection("Data Source=:memory:");
        await sqliteConnection.OpenAsync();

        var options = CreateOptions(sqliteConnection);

        await using (var setupContext = new AppDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        var correlationId = Id<CorrelationScope>.New();

        await using (var contextA = new AppDbContext(options))
        {
            contextA.CommandEnvelopes.Add(CreateEnvelope(correlationId, "first"));
            var unitOfWorkA = new EfUnitOfWork(contextA);
            await unitOfWorkA.SaveChangesAsync();
        }

        var duplicateEnvelope = CreateEnvelope(correlationId, "second");
        var user = new User
        {
            Id = Id<User>.New(),
            Name = "dedupe-user",
            Email = $"dedupe-{Guid.NewGuid():N}@example.com",
            ProfileRank = "Rookie"
        };

        await using (var contextB = new AppDbContext(options))
        {
            contextB.CommandEnvelopes.Add(duplicateEnvelope);
            contextB.Users.Add(user);
            var unitOfWorkB = new EfUnitOfWork(contextB);

            var saved = await unitOfWorkB.SaveChangesAsync();

            saved.Should().Be(1);
            contextB.Entry(duplicateEnvelope).State.Should().Be(EntityState.Detached);
        }

        await using var verificationContext = new AppDbContext(options);
        (await verificationContext.CommandEnvelopes.Where(x => x.CorrelationId == correlationId).CountAsync()).Should().Be(1);

        var persistedUser = await verificationContext.Users.SingleAsync(x => x.Id == user.Id);
        persistedUser.Email.Should().Be(user.Email);
    }

    private static DbContextOptions<AppDbContext> CreateOptions(SqliteConnection sqliteConnection)
        => new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(sqliteConnection)
            .Options;

    private static CommandEnvelope CreateEnvelope(Id<CorrelationScope> correlationId, string payloadSuffix)
        => new()
        {
            Id = Id<CommandEnvelope>.New(),
            CorrelationId = correlationId,
            CommandTypeFullName = "Test.Command",
            PayloadJson = $"{{\"value\":\"{payloadSuffix}\"}}",
            Status = ActionExecutionStatus.Pending,
            NextAttemptAt = DateTimeOffset.UtcNow
        };
}
