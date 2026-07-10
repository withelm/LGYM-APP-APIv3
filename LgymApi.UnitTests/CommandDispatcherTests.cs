using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using FluentAssertions;

namespace LgymApi.UnitTests;

/// <summary>
/// Tests for CommandDispatcher deterministic correlation ID generation.
/// </summary>
[TestFixture]
public sealed class CommandDispatcherTests
{
    private FakeCommandEnvelopeRepository _repository = null!;
    private FakeUnitOfWork _unitOfWork = null!;
    private Microsoft.Extensions.DependencyInjection.ServiceProvider _serviceProvider = null!;
    private FakeLogger _logger = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new FakeCommandEnvelopeRepository();
        _unitOfWork = new FakeUnitOfWork();
        _logger = new FakeLogger();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
    }

    [Test]
    public async Task ComputeDeterministicCorrelationId_IsDeterministic()
    {
        // Arrange
        var invitationId = Id<TrainerInvitation>.New();

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<CommandDispatcher>>(_ => _logger);
        services.AddScoped<IBackgroundAction<InvitationAcceptedCommand>, FakeInvitationAcceptedHandler>();
        _serviceProvider = services.BuildServiceProvider();

        var dispatcher = new CommandDispatcher(
            _serviceProvider,
            _repository,
            _unitOfWork,
            CreateTestDbContext(),
            _logger);

        // Act - Enqueue the same command 100 times
        var correlationIds = new List<Id<CorrelationScope>>();
        for (int i = 0; i < 100; i++)
        {
            var cmd = new InvitationAcceptedCommand { InvitationId = invitationId };
            await dispatcher.EnqueueAsync(cmd);
            
            // Get the correlation ID from the repository
            var envelopes = _repository.GetAllEnvelopes();
            var lastEnvelope = envelopes.LastOrDefault();
            if (lastEnvelope != null)
            {
                correlationIds.Add(lastEnvelope.CorrelationId);
            }
        }

        // Assert - All correlation IDs should be identical
        correlationIds.Should().HaveCount(100);
        correlationIds.Distinct().Should().HaveCount(1, "All 100 identical commands should produce the same correlation ID");
    }

    [Test]
    public async Task ComputeDeterministicCorrelationId_DifferentCommands_DifferentIds()
    {
        // Arrange
        var invitationId1 = Id<TrainerInvitation>.New();
        var invitationId2 = Id<TrainerInvitation>.New();

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<CommandDispatcher>>(_ => _logger);
        services.AddScoped<IBackgroundAction<InvitationAcceptedCommand>, FakeInvitationAcceptedHandler>();
        _serviceProvider = services.BuildServiceProvider();

        var dispatcher = new CommandDispatcher(
            _serviceProvider,
            _repository,
            _unitOfWork,
            CreateTestDbContext(),
            _logger);

        // Act - Enqueue commands with different InvitationIds
        var command1 = new InvitationAcceptedCommand { InvitationId = invitationId1 };
        var command2 = new InvitationAcceptedCommand { InvitationId = invitationId2 };

        await dispatcher.EnqueueAsync(command1);
        await dispatcher.EnqueueAsync(command2);

        // Assert - Should have two different correlation IDs
        var envelopes = _repository.GetAllEnvelopes();
        envelopes.Should().HaveCount(2);
        envelopes[0].CorrelationId.Should().NotBe(envelopes[1].CorrelationId, "Different InvitationIds should produce different correlation IDs");
    }

    [Test]
    public async Task EnqueueAsync_SameInvitationIdDifferentInstances_SameCorrelationId()
    {
        // Arrange
        var invitationId = Id<TrainerInvitation>.New();

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<CommandDispatcher>>(_ => _logger);
        services.AddScoped<IBackgroundAction<InvitationAcceptedCommand>, FakeInvitationAcceptedHandler>();
        _serviceProvider = services.BuildServiceProvider();

        var dispatcher = new CommandDispatcher(
            _serviceProvider,
            _repository,
            _unitOfWork,
            CreateTestDbContext(),
            _logger);

        // Act - Create two separate command instances with the same InvitationId
        var command1 = new InvitationAcceptedCommand { InvitationId = invitationId };
        var command2 = new InvitationAcceptedCommand { InvitationId = invitationId };

        await dispatcher.EnqueueAsync(command1);
        await dispatcher.EnqueueAsync(command2);

        // Assert - Should have only one envelope (idempotent)
        var envelopes = _repository.GetAllEnvelopes();
        envelopes.Should().HaveCount(1, "Same InvitationId should produce same correlation ID and be deduplicated");
    }

    // Test handler
    private sealed class FakeInvitationAcceptedHandler : IBackgroundAction<InvitationAcceptedCommand>
    {
        public Task ExecuteAsync(InvitationAcceptedCommand command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    // Fake implementations
    private sealed class FakeCommandEnvelopeRepository : ICommandEnvelopeRepository
    {
        private readonly Dictionary<Id<CommandEnvelope>, CommandEnvelope> _envelopes = new();
        public int UpdateCallCount { get; private set; }

        public void AddEnvelope(CommandEnvelope envelope)
        {
            _envelopes[envelope.Id] = envelope;
        }

        public Task AddAsync(CommandEnvelope envelope, CancellationToken cancellationToken = default)
        {
            _envelopes[envelope.Id] = envelope;
            return Task.CompletedTask;
        }

        public Task<CommandEnvelope?> FindByIdAsync(Id<CommandEnvelope> id, CancellationToken cancellationToken = default)
        {
            _envelopes.TryGetValue(id, out var envelope);
            return Task.FromResult(envelope);
        }

        public Task<CommandEnvelope?> FindByCorrelationIdAsync(Id<CorrelationScope> correlationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_envelopes.Values.FirstOrDefault(e => e.CorrelationId == correlationId));
        }

        public Task<List<CommandEnvelope>> GetPendingRetriesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_envelopes.Values
                .Where(e => e.Status == ActionExecutionStatus.Failed && e.NextAttemptAt <= DateTimeOffset.UtcNow)
                .ToList());
        }

        public Task UpdateAsync(CommandEnvelope envelope, CancellationToken cancellationToken = default)
        {
            UpdateCallCount++;
            return Task.CompletedTask;
        }

        public Task<CommandEnvelope> AddOrGetExistingAsync(CommandEnvelope envelope, CancellationToken cancellationToken = default)
        {
            var existing = _envelopes.Values.FirstOrDefault(e => e.CorrelationId == envelope.CorrelationId);
            if (existing != null)
            {
                return Task.FromResult(existing);
            }
            
            _envelopes[envelope.Id] = envelope;
            return Task.FromResult(envelope);
        }

        public Task<List<CommandEnvelope>> GetPendingUndispatchedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_envelopes.Values.Where(e => e.Status == ActionExecutionStatus.Pending && e.DispatchedAt == null).ToList());

        public Task<List<CommandEnvelope>> GetFailedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_envelopes.Values.Where(e => e.Status == ActionExecutionStatus.Failed).ToList());

        public Task<List<CommandEnvelope>> GetDeadLetteredAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_envelopes.Values.Where(e => e.Status == ActionExecutionStatus.DeadLettered).ToList());

        public Task<int> CountByStatusAsync(ActionExecutionStatus status, CancellationToken cancellationToken = default)
            => Task.FromResult(_envelopes.Values.Count(e => e.Status == status));

        public Task<int> DeleteCompletedOlderThanAsync(DateTimeOffset cutoffDate, CancellationToken cancellationToken = default)
        {
            var toDelete = _envelopes.Values
                .Where(e => e.Status == ActionExecutionStatus.Completed && e.CompletedAt.HasValue && e.CompletedAt < cutoffDate)
                .ToList();
            
            var count = toDelete.Count;
            foreach (var e in toDelete)
            {
                _envelopes.Remove(e.Id);
            }

            return Task.FromResult(count);
        }

        public List<CommandEnvelope> GetAllEnvelopes() => _envelopes.Values.ToList();
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
            => throw new NotSupportedException();

        public void DetachEntity<TEntity>(TEntity entity) where TEntity : class { }
    }

    private AppDbContext CreateTestDbContext()
    {
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("TestDb_" + Id<CommandEnvelope>.New().ToString())
            .Options);
    }

    private sealed class FakeLogger : ILogger<CommandDispatcher>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
