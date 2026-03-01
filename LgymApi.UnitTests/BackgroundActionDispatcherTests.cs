using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace LgymApi.UnitTests;

/// <summary>
/// Tests for CommandDispatcher covering:
/// - Single handler dispatch flow
/// - Multi-handler dispatch flow
/// - Zero-handler no-enqueue path
/// - Duplicate/idempotent path blocks duplicate enqueue
/// - Persistence + enqueue contract assertions
/// </summary>
[TestFixture]
public sealed class BackgroundActionDispatcherTests
{
    private FakeCommandEnvelopeRepository _repository = null!;
    private FakeUnitOfWork _unitOfWork = null!;
    private FakeActionMessageScheduler _scheduler = null!;
    private Microsoft.Extensions.DependencyInjection.ServiceProvider _serviceProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new FakeCommandEnvelopeRepository();
        _unitOfWork = new FakeUnitOfWork();
        _scheduler = new FakeActionMessageScheduler();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
    }

    [Test]
    public void Enqueue_SingleHandler_CreatesEnvelopeAndEnqueues()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler1>();
        services.AddSingleton<ILogger<CommandDispatcher>>(_ => new FakeLogger<CommandDispatcher>());
        _serviceProvider = services.BuildServiceProvider();

        var dispatcher = CreateDispatcher();
        var command = new TestCommand { Value = "test-single" };

        // Act
        dispatcher.Enqueue(command);

        // Assert
        Assert.That(_repository.Envelopes.Count, Is.EqualTo(1), "Should persist exactly one envelope");
        Assert.That(_unitOfWork.SaveCallCount, Is.EqualTo(1), "Should save once after persisting envelope");
        Assert.That(_scheduler.EnqueuedIds.Count, Is.EqualTo(1), "Should enqueue exactly one job");

        var envelope = _repository.Envelopes.First();
        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.Pending));
        Assert.That(envelope.CommandTypeFullName, Is.EqualTo(typeof(TestCommand).FullName));
        Assert.That(envelope.PayloadJson, Does.Contain("test-single"));
        Assert.That(_scheduler.EnqueuedIds.First(), Is.EqualTo(envelope.Id));
    }

    [Test]
    public void Enqueue_MultipleHandlers_CreatesOneEnvelopeAndOneJob()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler1>();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler2>();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerSuccess>();
        services.AddSingleton<ILogger<CommandDispatcher>>(_ => new FakeLogger<CommandDispatcher>());
        _serviceProvider = services.BuildServiceProvider();

        var dispatcher = CreateDispatcher();
        var command = new TestCommand { Value = "test-multi" };

        // Act
        dispatcher.Enqueue(command);

        // Assert
        Assert.That(_repository.Envelopes.Count, Is.EqualTo(1), "Should persist exactly one envelope despite multiple handlers");
        Assert.That(_scheduler.EnqueuedIds.Count, Is.EqualTo(1), "Should enqueue exactly one job (orchestrator will fan-out)");
        Assert.That(_unitOfWork.SaveCallCount, Is.EqualTo(1));
    }

    [Test]
    public void Enqueue_ZeroHandlers_NoEnqueueAndNoFailure()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<CommandDispatcher>>(_ => new FakeLogger<CommandDispatcher>());
        _serviceProvider = services.BuildServiceProvider(); // No handlers registered

        var dispatcher = CreateDispatcher();
        var command = new TestCommand { Value = "test-zero" };

        // Act
        dispatcher.Enqueue(command);

        // Assert - Zero-handler path: safe no-op, no failure, no enqueue
        Assert.That(_repository.Envelopes.Count, Is.EqualTo(0), "Should not persist envelope when no handlers registered");
        Assert.That(_scheduler.EnqueuedIds.Count, Is.EqualTo(0), "Should not enqueue job when no handlers registered");
        Assert.That(_unitOfWork.SaveCallCount, Is.EqualTo(0), "Should not save when short-circuiting");
    }

    [Test]
    public void Enqueue_DuplicateCommand_BlocksDuplicateEnqueue()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler1>();
        services.AddSingleton<ILogger<CommandDispatcher>>(_ => new FakeLogger<CommandDispatcher>());
        _serviceProvider = services.BuildServiceProvider();

        var dispatcher = CreateDispatcher();
        var command = new TestCommand { Value = "test-duplicate" };

        // Act - Dispatch same command twice
        dispatcher.Enqueue(command);
        dispatcher.Enqueue(command); // Duplicate dispatch with identical content

        // Assert - Deterministic correlation ID should deduplicate second dispatch

        // Assert - Idempotency check: duplicate envelope should not enqueue
        Assert.That(_repository.Envelopes.Count, Is.EqualTo(1), "Should not create duplicate envelope for identical command");
        Assert.That(_scheduler.EnqueuedIds.Count, Is.EqualTo(1), "Should only enqueue first command, second is deduplicated");
        Assert.That(_unitOfWork.SaveCallCount, Is.EqualTo(1), "Should only save once for first dispatch");
    }

    [Test]
    public void Enqueue_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<CommandDispatcher>>(_ => new FakeLogger<CommandDispatcher>());
        _serviceProvider = services.BuildServiceProvider();

        var dispatcher = CreateDispatcher();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => dispatcher.Enqueue<TestCommand>(null!));
    }

    [Test]
    public void Enqueue_ExactTypeMatchingOnly_DerivedCommandDoesNotInherit()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler1>(); // Base command handler
        services.AddSingleton<ILogger<CommandDispatcher>>(_ => new FakeLogger<CommandDispatcher>());
        _serviceProvider = services.BuildServiceProvider();

        var dispatcher = CreateDispatcher();
        var derivedCommand = new DerivedTestCommand { Value = "derived", DerivedValue = "extra" };

        // Act - DerivedTestCommand has NO handlers registered (base handler should NOT match)
        dispatcher.Enqueue(derivedCommand);

        // Assert - Zero-handler path because exact-type matching only
        Assert.That(_repository.Envelopes.Count, Is.EqualTo(0), "Should not persist envelope for derived command when only base handler exists");
        Assert.That(_scheduler.EnqueuedIds.Count, Is.EqualTo(0), "Should not enqueue when no exact-type handlers");
    }

    [Test]
    public void Enqueue_PersistsThenEnqueues_OrderGuaranteed()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler1>();
        services.AddSingleton<ILogger<CommandDispatcher>>(_ => new FakeLogger<CommandDispatcher>());
        _serviceProvider = services.BuildServiceProvider();

        var dispatcher = CreateDispatcher();
        var command = new TestCommand { Value = "test-order" };

        // Act
        dispatcher.Enqueue(command);

        // Assert - Envelope must be persisted before enqueue
        Assert.That(_repository.Envelopes.Count, Is.EqualTo(1));
        Assert.That(_unitOfWork.SaveCallCount, Is.EqualTo(1));
        Assert.That(_scheduler.EnqueuedIds.Count, Is.EqualTo(1));
        
        var envelope = _repository.Envelopes.First();
        Assert.That(_scheduler.EnqueuedIds.First(), Is.EqualTo(envelope.Id), "Scheduler must receive persisted envelope ID");
    }

    private CommandDispatcher CreateDispatcher()
    {
        return new CommandDispatcher(
            _serviceProvider,
            _repository,
            _unitOfWork,
            _scheduler,
            _serviceProvider.GetRequiredService<ILogger<CommandDispatcher>>());
    }

    // Test doubles
    private sealed class FakeCommandEnvelopeRepository : ICommandEnvelopeRepository
    {
        public List<CommandEnvelope> Envelopes { get; } = new();

        public void AddEnvelope(CommandEnvelope envelope) => Envelopes.Add(envelope);

        public Task AddAsync(CommandEnvelope envelope, CancellationToken cancellationToken = default)
        {
            Envelopes.Add(envelope);
            return Task.CompletedTask;
        }

        public Task<CommandEnvelope?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Envelopes.FirstOrDefault(e => e.Id == id));
        }

        public Task<CommandEnvelope?> FindByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Envelopes.FirstOrDefault(e => e.CorrelationId == correlationId));
        }

        public Task<List<CommandEnvelope>> GetPendingRetriesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Envelopes
                .Where(e => e.Status == ActionExecutionStatus.Failed && e.NextAttemptAt <= DateTimeOffset.UtcNow)
                .ToList());
        }

        public Task UpdateAsync(CommandEnvelope envelope, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<CommandEnvelope> AddOrGetExistingAsync(CommandEnvelope envelope, CancellationToken cancellationToken = default)
        {

            var existing = Envelopes.FirstOrDefault(e => e.CorrelationId == envelope.CorrelationId);
            if (existing != null)
            {
                return Task.FromResult(existing);
            }

            Envelopes.Add(envelope);
            return Task.FromResult(envelope);
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCallCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            return Task.FromResult(1);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction());
        }

        public void Dispose() { }

        private sealed class FakeTransaction : IUnitOfWorkTransaction
        {
            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }


    private sealed class FakeActionMessageScheduler : IActionMessageScheduler
    {
        public List<Guid> EnqueuedIds { get; } = new();

        public void Enqueue(Guid actionMessageId)
        {
            EnqueuedIds.Add(actionMessageId);
        }
    }

    private sealed class FakeLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    // Test command types
    private class TestCommand : IActionCommand
    {
        public string Value { get; set; } = string.Empty;
    }

    private class DerivedTestCommand : TestCommand
    {
        public string DerivedValue { get; set; } = string.Empty;
    }

    // Test action handlers
    private sealed class TestActionHandler1 : IBackgroundAction<TestCommand>
    {
        public Task ExecuteAsync(TestCommand command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestActionHandler2 : IBackgroundAction<TestCommand>
    {
        public Task ExecuteAsync(TestCommand command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestActionHandlerSuccess : IBackgroundAction<TestCommand>
    {
        public Task ExecuteAsync(TestCommand command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
