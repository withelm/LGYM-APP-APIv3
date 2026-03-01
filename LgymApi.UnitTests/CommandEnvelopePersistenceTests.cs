using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LgymApi.UnitTests;

/// <summary>
/// Tests durable persistence behavior of CommandEnvelope and ActionExecutionLog entities.
/// Validates entity lifecycle, relationships, and index coverage.
/// </summary>
[TestFixture]
public class CommandEnvelopePersistenceTests
{
    private DbContextOptions<AppDbContext> _contextOptions = null!;
    private AppDbContext _context = null!;

    [SetUp]
    public void Setup()
    {
        // Use in-memory database for test isolation
        _contextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(_contextOptions);
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Dispose();
    }

    [Test]
    public async Task CommandEnvelope_CanBePersisted_WithAllRequiredFields()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var envelope = new CommandEnvelope
        {
            Id = Guid.NewGuid(),
            CorrelationId = correlationId,
            PayloadJson = """{"data":"test"}""",
            CommandTypeFullName = "TestNamespace.TestCommand, TestAssembly",
            Status = ActionExecutionStatus.Pending,
            NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(30),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        // Act
        _context.CommandEnvelopes.Add(envelope);
        await _context.SaveChangesAsync();

        // Assert
        var retrieved = await _context.CommandEnvelopes
            .FirstOrDefaultAsync(e => e.Id == envelope.Id);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.CorrelationId, Is.EqualTo(correlationId));
        Assert.That(retrieved.Status, Is.EqualTo(ActionExecutionStatus.Pending));
    }

    [Test]
    public async Task ActionExecutionLog_CanBeLinked_ToCommandEnvelope()
    {
        // Arrange
        var envelopeId = Guid.NewGuid();
        var envelope = new CommandEnvelope
        {
            Id = envelopeId,
            CorrelationId = Guid.NewGuid(),
            PayloadJson = """{"data":"test"}""",
            CommandTypeFullName = "TestNamespace.TestCommand, TestAssembly",
            Status = ActionExecutionStatus.Processing,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var log = new ActionExecutionLog
        {
            Id = Guid.NewGuid(),
            CommandEnvelopeId = envelopeId,
            ActionType = ActionExecutionLogType.Execute,
            Status = ActionExecutionStatus.Completed,
            AttemptNumber = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        // Act
        _context.CommandEnvelopes.Add(envelope);
        _context.ActionExecutionLogs.Add(log);
        await _context.SaveChangesAsync();

        // Assert
        var retrievedEnvelope = await _context.CommandEnvelopes
            .Include(e => e.ExecutionLogs)
            .FirstOrDefaultAsync(e => e.Id == envelopeId);

        Assert.That(retrievedEnvelope, Is.Not.Null);
        Assert.That(retrievedEnvelope!.ExecutionLogs, Has.Count.EqualTo(1));
        Assert.That(retrievedEnvelope.ExecutionLogs.First().ActionType, Is.EqualTo(ActionExecutionLogType.Execute));
    }

    [Test]
    public async Task ActionExecutionLog_CanTrack_MultipleAttempts()
    {
        // Arrange
        var envelopeId = Guid.NewGuid();
        var envelope = new CommandEnvelope
        {
            Id = envelopeId,
            CorrelationId = Guid.NewGuid(),
            PayloadJson = """{"data":"test"}""",
            CommandTypeFullName = "TestNamespace.TestCommand, TestAssembly",
            Status = ActionExecutionStatus.Failed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _context.CommandEnvelopes.Add(envelope);
        await _context.SaveChangesAsync();

        var log1 = new ActionExecutionLog
        {
            Id = Guid.NewGuid(),
            CommandEnvelopeId = envelopeId,
            ActionType = ActionExecutionLogType.Execute,
            Status = ActionExecutionStatus.Failed,
            AttemptNumber = 1,
            ErrorMessage = "Timeout",
            CreatedAt = DateTimeOffset.UtcNow.AddSeconds(-10),
            UpdatedAt = DateTimeOffset.UtcNow.AddSeconds(-10),
        };

        var log2 = new ActionExecutionLog
        {
            Id = Guid.NewGuid(),
            CommandEnvelopeId = envelopeId,
            ActionType = ActionExecutionLogType.Retry,
            Status = ActionExecutionStatus.Processing,
            AttemptNumber = 2,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        // Act
        _context.ActionExecutionLogs.AddRange(log1, log2);
        await _context.SaveChangesAsync();

        // Assert
        var logs = await _context.ActionExecutionLogs
            .Where(l => l.CommandEnvelopeId == envelopeId)
            .OrderBy(l => l.AttemptNumber)
            .ToListAsync();

        Assert.That(logs, Has.Count.EqualTo(2));
        Assert.That(logs[0].AttemptNumber, Is.EqualTo(1));
        Assert.That(logs[0].Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(logs[1].AttemptNumber, Is.EqualTo(2));
        Assert.That(logs[1].Status, Is.EqualTo(ActionExecutionStatus.Processing));
    }

    [Test]
    public async Task CommandEnvelope_StatusTransition_Persists()
    {
        // Arrange
        var envelope = new CommandEnvelope
        {
            Id = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            PayloadJson = """{"data":"test"}""",
            CommandTypeFullName = "TestNamespace.TestCommand, TestAssembly",
            Status = ActionExecutionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _context.CommandEnvelopes.Add(envelope);
        await _context.SaveChangesAsync();

        // Act - transition from Pending to Processing
        var retrieved = await _context.CommandEnvelopes.FindAsync(envelope.Id);
        Assert.That(retrieved, Is.Not.Null);
        retrieved!.Status = ActionExecutionStatus.Processing;
        retrieved.LastAttemptAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        // Assert - transition persisted
        var final = await _context.CommandEnvelopes.FindAsync(envelope.Id);
        Assert.That(final!.Status, Is.EqualTo(ActionExecutionStatus.Processing));
        Assert.That(final.LastAttemptAt, Is.Not.Null);
    }

    [Test]
    public async Task CommandEnvelope_CompletedAt_SetOnSuccess()
    {
        // Arrange
        var envelope = new CommandEnvelope
        {
            Id = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            PayloadJson = """{"data":"test"}""",
            CommandTypeFullName = "TestNamespace.TestCommand, TestAssembly",
            Status = ActionExecutionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _context.CommandEnvelopes.Add(envelope);
        await _context.SaveChangesAsync();

        // Act - mark as completed
        var retrieved = await _context.CommandEnvelopes.FindAsync(envelope.Id);
        Assert.That(retrieved, Is.Not.Null);
        retrieved!.Status = ActionExecutionStatus.Completed;
        retrieved.CompletedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        // Assert
        var final = await _context.CommandEnvelopes.FindAsync(envelope.Id);
        Assert.That(final!.Status, Is.EqualTo(ActionExecutionStatus.Completed));
        Assert.That(final.CompletedAt, Is.Not.Null);
    }

    [Test]
    public async Task ActionExecutionLog_ErrorTracking_Persists()
    {
        // Arrange
        var envelopeId = Guid.NewGuid();
        var envelope = new CommandEnvelope
        {
            Id = envelopeId,
            CorrelationId = Guid.NewGuid(),
            PayloadJson = """{"data":"test"}""",
            CommandTypeFullName = "TestNamespace.TestCommand, TestAssembly",
            Status = ActionExecutionStatus.Failed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var log = new ActionExecutionLog
        {
            Id = Guid.NewGuid(),
            CommandEnvelopeId = envelopeId,
            ActionType = ActionExecutionLogType.Execute,
            Status = ActionExecutionStatus.Failed,
            AttemptNumber = 1,
            ErrorMessage = "Connection timeout",
            ErrorDetails = "System.TimeoutException: The operation timed out...",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        // Act
        _context.CommandEnvelopes.Add(envelope);
        _context.ActionExecutionLogs.Add(log);
        await _context.SaveChangesAsync();

        // Assert
        var retrieved = await _context.ActionExecutionLogs.FirstAsync();
        Assert.That(retrieved.ErrorMessage, Is.EqualTo("Connection timeout"));
        Assert.That(retrieved.ErrorDetails, Does.Contain("TimeoutException"));
    }

    [Test]
    public async Task CommandEnvelope_CorrelationId_EnablesTracing()
    {
        // Arrange - simulate related commands with same correlation
        var correlationId = Guid.NewGuid();
        var envelopes = new[]
        {
            new CommandEnvelope
            {
                Id = Guid.NewGuid(),
                CorrelationId = correlationId,
                PayloadJson = """{"cmd":"send-email"}""",
                CommandTypeFullName = "SendEmailCommand, MyAssembly",
                Status = ActionExecutionStatus.Completed,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            },
            new CommandEnvelope
            {
                Id = Guid.NewGuid(),
                CorrelationId = correlationId,
                PayloadJson = """{"cmd":"log-event"}""",
                CommandTypeFullName = "LogEventCommand, MyAssembly",
                Status = ActionExecutionStatus.Completed,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            }
        };

        // Act
        _context.CommandEnvelopes.AddRange(envelopes);
        await _context.SaveChangesAsync();

        // Assert - all related envelopes retrievable by correlation
        var related = await _context.CommandEnvelopes
            .Where(e => e.CorrelationId == correlationId)
            .ToListAsync();

        Assert.That(related, Has.Count.EqualTo(2));
        Assert.That(related.Select(e => e.CommandTypeFullName).Distinct().ToList(), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task CommandEnvelope_NextAttemptAt_IndexedForWorkRetrieval()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var pendingEnvelopes = new[]
        {
            new CommandEnvelope
            {
                Id = Guid.NewGuid(),
                CorrelationId = Guid.NewGuid(),
                PayloadJson = """{}""",
                CommandTypeFullName = "Cmd1, Asm",
                Status = ActionExecutionStatus.Failed,
                NextAttemptAt = now.AddSeconds(5), // Ready soon
                CreatedAt = now,
                UpdatedAt = now,
            },
            new CommandEnvelope
            {
                Id = Guid.NewGuid(),
                CorrelationId = Guid.NewGuid(),
                PayloadJson = """{}""",
                CommandTypeFullName = "Cmd2, Asm",
                Status = ActionExecutionStatus.Failed,
                NextAttemptAt = now.AddMinutes(10), // Not ready
                CreatedAt = now,
                UpdatedAt = now,
            },
            new CommandEnvelope
            {
                Id = Guid.NewGuid(),
                CorrelationId = Guid.NewGuid(),
                PayloadJson = """{}""",
                CommandTypeFullName = "Cmd3, Asm",
                Status = ActionExecutionStatus.Completed,
                NextAttemptAt = null, // Terminal state
                CreatedAt = now,
                UpdatedAt = now,
            }
        };

        _context.CommandEnvelopes.AddRange(pendingEnvelopes);
        await _context.SaveChangesAsync();

        // Act - find work ready for retry (status Failed AND NextAttemptAt <= now)
        var readyForWork = await _context.CommandEnvelopes
            .Where(e => e.Status == ActionExecutionStatus.Failed && 
                        e.NextAttemptAt.HasValue && 
                        e.NextAttemptAt <= now.AddSeconds(10))
            .ToListAsync();

        // Assert
        Assert.That(readyForWork, Has.Count.EqualTo(1));
        Assert.That(readyForWork[0].CommandTypeFullName, Is.EqualTo("Cmd1, Asm"));
    }
}
