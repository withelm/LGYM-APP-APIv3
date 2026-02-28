using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LgymApi.UnitTests;

/// <summary>
/// Tests CommandEnvelopeRepository and ExecutionLogRepository from Task 9 infrastructure.
/// Named to match BackgroundActionMessageRepository filter for QA discoverability.
/// </summary>
[TestFixture]
public sealed class BackgroundActionMessageRepositoryTests
{
    [Test]
    public async Task CommandEnvelope_AddAsync_AddsEnvelopeToContext()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"command-envelope-repo-{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new AppDbContext(options);
        var repository = new CommandEnvelopeRepository(dbContext);

        var envelope = new CommandEnvelope
        {
            CorrelationId = Guid.NewGuid(),
            CommandTypeFullName = "TestNamespace.TestCommand, TestAssembly",
            PayloadJson = "{\"test\":\"data\"}",
            Status = ActionExecutionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Act
        await repository.AddAsync(envelope);
        await dbContext.SaveChangesAsync();

        // Assert
        var saved = await dbContext.CommandEnvelopes.FindAsync(envelope.Id);
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved.CommandTypeFullName, Is.EqualTo("TestNamespace.TestCommand, TestAssembly"));
        Assert.That(saved.Status, Is.EqualTo(ActionExecutionStatus.Pending));
    }

    [Test]
    public async Task CommandEnvelope_FindByIdAsync_ReturnsEnvelopeWhenExists()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"command-envelope-repo-{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new AppDbContext(options);
        var repository = new CommandEnvelopeRepository(dbContext);

        var envelope = new CommandEnvelope
        {
            CorrelationId = Guid.NewGuid(),
            CommandTypeFullName = "TestNamespace.TestCommand, TestAssembly",
            PayloadJson = "{}",
            Status = ActionExecutionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await repository.AddAsync(envelope);
        await dbContext.SaveChangesAsync();

        // Act
        var found = await repository.FindByIdAsync(envelope.Id);

        // Assert
        Assert.That(found, Is.Not.Null);
        Assert.That(found.Id, Is.EqualTo(envelope.Id));
    }

    [Test]
    public async Task CommandEnvelope_FindByIdAsync_ReturnsNullWhenNotFound()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"command-envelope-repo-{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new AppDbContext(options);
        var repository = new CommandEnvelopeRepository(dbContext);

        // Act
        var found = await repository.FindByIdAsync(Guid.NewGuid());

        // Assert
        Assert.That(found, Is.Null);
    }

    [Test]
    public async Task CommandEnvelope_FindByCorrelationIdAsync_ReturnsEnvelopeWhenExists()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"command-envelope-repo-{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new AppDbContext(options);
        var repository = new CommandEnvelopeRepository(dbContext);

        var correlationId = Guid.NewGuid();
        var envelope = new CommandEnvelope
        {
            CorrelationId = correlationId,
            CommandTypeFullName = "TestNamespace.TestCommand, TestAssembly",
            PayloadJson = "{}",
            Status = ActionExecutionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await repository.AddAsync(envelope);
        await dbContext.SaveChangesAsync();

        // Act
        var found = await repository.FindByCorrelationIdAsync(correlationId);

        // Assert
        Assert.That(found, Is.Not.Null);
        Assert.That(found.CorrelationId, Is.EqualTo(correlationId));
    }

    [Test]
    public async Task CommandEnvelope_FindByCorrelationIdAsync_ReturnsNullWhenNotFound()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"command-envelope-repo-{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new AppDbContext(options);
        var repository = new CommandEnvelopeRepository(dbContext);

        // Act
        var found = await repository.FindByCorrelationIdAsync(Guid.NewGuid());

        // Assert
        Assert.That(found, Is.Null);
    }

    [Test]
    public async Task CommandEnvelope_GetPendingRetriesAsync_ReturnsFailedEnvelopesReadyForRetry()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"command-envelope-repo-{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new AppDbContext(options);
        var repository = new CommandEnvelopeRepository(dbContext);

        var readyForRetry = new CommandEnvelope
        {
            CorrelationId = Guid.NewGuid(),
            CommandTypeFullName = "TestNamespace.TestCommand, TestAssembly",
            PayloadJson = "{}",
            Status = ActionExecutionStatus.Failed,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            UpdatedAt = DateTimeOffset.UtcNow,
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        var notYetReady = new CommandEnvelope
        {
            CorrelationId = Guid.NewGuid(),
            CommandTypeFullName = "TestNamespace.TestCommand, TestAssembly",
            PayloadJson = "{}",
            Status = ActionExecutionStatus.Failed,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            UpdatedAt = DateTimeOffset.UtcNow,
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(30)
        };

        var succeeded = new CommandEnvelope
        {
            CorrelationId = Guid.NewGuid(),
            CommandTypeFullName = "TestNamespace.TestCommand, TestAssembly",
            PayloadJson = "{}",
            Status = ActionExecutionStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-3),
            UpdatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow.AddHours(-2)
        };

        await repository.AddAsync(readyForRetry);
        await repository.AddAsync(notYetReady);
        await repository.AddAsync(succeeded);
        await dbContext.SaveChangesAsync();

        // Act
        var pending = await repository.GetPendingRetriesAsync();

        // Assert
        Assert.That(pending, Has.Count.EqualTo(1));
        Assert.That(pending.First().Id, Is.EqualTo(readyForRetry.Id));
    }

    [Test]
    public async Task CommandEnvelope_UpdateAsync_PersistsChanges()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"command-envelope-repo-{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new AppDbContext(options);
        var repository = new CommandEnvelopeRepository(dbContext);

        var envelope = new CommandEnvelope
        {
            CorrelationId = Guid.NewGuid(),
            CommandTypeFullName = "TestNamespace.TestCommand, TestAssembly",
            PayloadJson = "{}",
            Status = ActionExecutionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await repository.AddAsync(envelope);
        await dbContext.SaveChangesAsync();
        await dbContext.SaveChangesAsync();

        // Act
        envelope.Status = ActionExecutionStatus.Completed;
        envelope.CompletedAt = DateTimeOffset.UtcNow;
        envelope.UpdatedAt = DateTimeOffset.UtcNow;
        await repository.UpdateAsync(envelope);
        await dbContext.SaveChangesAsync();

        // Assert
        var updated = await dbContext.CommandEnvelopes.FindAsync(envelope.Id);
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated.Status, Is.EqualTo(ActionExecutionStatus.Completed));
        Assert.That(updated.CompletedAt, Is.Not.Null);
    }

    [Test]
    public async Task ExecutionLog_AddAsync_AddsLogToContext()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"execution-log-repo-{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new AppDbContext(options);
        var repository = new ExecutionLogRepository(dbContext);

        var log = new ExecutionLog
        {
            CommandEnvelopeId = Guid.NewGuid(),
            ActionType = "Execute",
            AttemptNumber = 1,
            Status = ActionExecutionStatus.Processing,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Act
        await repository.AddAsync(log);
        await dbContext.SaveChangesAsync();

        // Assert
        var saved = await dbContext.ExecutionLogs.FindAsync(log.Id);
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved.AttemptNumber, Is.EqualTo(1));
        Assert.That(saved.Status, Is.EqualTo(ActionExecutionStatus.Processing));
    }

    [Test]
    public async Task ExecutionLog_GetByCommandEnvelopeIdAsync_ReturnsLogsOrderedByAttempt()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"execution-log-repo-{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new AppDbContext(options);
        var repository = new ExecutionLogRepository(dbContext);

        var envelopeId = Guid.NewGuid();

        var log3 = new ExecutionLog
        {
            CommandEnvelopeId = envelopeId,
            ActionType = "Execute",
            AttemptNumber = 3,
            Status = ActionExecutionStatus.Failed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var log1 = new ExecutionLog
        {
            CommandEnvelopeId = envelopeId,
            ActionType = "Execute",
            AttemptNumber = 1,
            Status = ActionExecutionStatus.Failed,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        var log2 = new ExecutionLog
        {
            CommandEnvelopeId = envelopeId,
            ActionType = "Execute",
            AttemptNumber = 2,
            Status = ActionExecutionStatus.Failed,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        await repository.AddAsync(log3);
        await repository.AddAsync(log1);
        await repository.AddAsync(log2);
        await dbContext.SaveChangesAsync();

        // Act
        var logs = await repository.GetByCommandEnvelopeIdAsync(envelopeId);

        // Assert
        Assert.That(logs, Has.Count.EqualTo(3));
        Assert.That(logs.ElementAt(0).AttemptNumber, Is.EqualTo(1));
        Assert.That(logs.ElementAt(1).AttemptNumber, Is.EqualTo(2));
        Assert.That(logs.ElementAt(2).AttemptNumber, Is.EqualTo(3));
    }

    [Test]
    public async Task ExecutionLog_GetByCommandEnvelopeIdAsync_ReturnsEmptyWhenNoLogs()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"execution-log-repo-{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new AppDbContext(options);
        var repository = new ExecutionLogRepository(dbContext);

        // Act
        var logs = await repository.GetByCommandEnvelopeIdAsync(Guid.NewGuid());

        // Assert
        Assert.That(logs, Is.Empty);
    }

    [Test]
    public async Task ExecutionLog_GetByCommandEnvelopeIdAsync_IsolatesLogsByEnvelopeId()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"execution-log-repo-{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new AppDbContext(options);
        var repository = new ExecutionLogRepository(dbContext);

        var envelopeId1 = Guid.NewGuid();
        var envelopeId2 = Guid.NewGuid();

        await repository.AddAsync(new ExecutionLog
        {
            CommandEnvelopeId = envelopeId1,
            ActionType = "Execute",
            AttemptNumber = 1,
            Status = ActionExecutionStatus.Processing,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await repository.AddAsync(new ExecutionLog
        {
            CommandEnvelopeId = envelopeId2,
            ActionType = "Execute",
            AttemptNumber = 1,
            Status = ActionExecutionStatus.Processing,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        // Act
        var logs = await repository.GetByCommandEnvelopeIdAsync(envelopeId1);

        // Assert
        Assert.That(logs, Has.Count.EqualTo(1));
        Assert.That(logs.First().CommandEnvelopeId, Is.EqualTo(envelopeId1));
    }
}
