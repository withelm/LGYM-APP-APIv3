using LgymApi.Domain.ValueObjects;
using System.Text.Json;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace LgymApi.UnitTests;

/// <summary>
/// Tests for BackgroundActionOrchestratorService covering:
/// - Happy path: multiple handlers succeed in parallel
/// - Failure path: one handler fails while others continue
/// - Retry behavior with attempt increment
/// - Dead-letter transition after max attempts with full error context
/// - Exact-type-only matching (base/derived mismatch rejected)
/// - Zero-handler path behavior
/// </summary>
[TestFixture]
public sealed class BackgroundActionOrchestratorTests
{
    private FakeCommandEnvelopeRepository _repository = null!;
    private FakeUnitOfWork _unitOfWork = null!;
    private Microsoft.Extensions.DependencyInjection.ServiceProvider _serviceProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new FakeCommandEnvelopeRepository();
        _unitOfWork = new FakeUnitOfWork();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
    }

    [Test]
    public async Task OrchestrateAsync_HappyPath_TwoHandlersSucceed()
    {
        // Arrange
         var envelopeId = Id<CommandEnvelope>.New();
        var command = new TestCommand { Value = "test" };
        var envelope = CreateEnvelope(envelopeId, command);
        _repository.AddEnvelope(envelope);

        var services = new ServiceCollection();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler1>();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler2>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert
        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.Completed));
        Assert.That(envelope.CompletedAt, Is.Not.Null);
        Assert.That(_unitOfWork.SaveCallCount, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task OrchestrateAsync_OneHandlerFails_OthersContinue()
    {
        // Arrange
         var envelopeId = Id<CommandEnvelope>.New();
        var command = new TestCommand { Value = "test" };
        var envelope = CreateEnvelope(envelopeId, command);
        _repository.AddEnvelope(envelope);

        var services = new ServiceCollection();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerSuccess>();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerFailure>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();
        var startedAt = DateTimeOffset.UtcNow;

        // Act & Assert - Should throw because one handler failed
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await orchestrator.OrchestrateAsync((Id<CommandEnvelope>)envelopeId));
        
        // Assert - Failure recorded and exception thrown for Hangfire retry
        Assert.That(ex!.Message, Does.Contain("Retry scheduled"));
        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(envelope.ExecutionLogs.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(envelope.NextAttemptAt, Is.Not.Null);
        Assert.That(envelope.NextAttemptAt!.Value - startedAt,
            Is.EqualTo(TimeSpan.FromSeconds(60)).Within(TimeSpan.FromSeconds(2)),
            "First failed orchestration should schedule first retry delay even with multiple handlers.");
    }

    [Test]
    public async Task OrchestrateAsync_RetryIncrement_AfterFailure()
    {
        // Arrange
         var envelopeId = Id<CommandEnvelope>.New();
        var command = new TestCommand { Value = "test" };
        var envelope = CreateEnvelope(envelopeId, command);
        _repository.AddEnvelope(envelope);

        var services = new ServiceCollection();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerFailure>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await orchestrator.OrchestrateAsync((Id<CommandEnvelope>)envelopeId));
        
        // Assert - Failure recorded, retry scheduled, exception thrown
        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(envelope.LastAttemptAt, Is.Not.Null);
        Assert.That(envelope.NextAttemptAt, Is.Not.Null);
    }

    [Test]
    public async Task OrchestrateAsync_DeadLettered_AfterMaxAttempts()
    {
        // Arrange
         var envelopeId = Id<CommandEnvelope>.New();
        var command = new TestCommand { Value = "test" };
        var envelope = CreateEnvelope(envelopeId, command);
        
        // Simulate 3 prior failed executions so current run exceeds retry limit.
        AddExecuteAttemptLog(envelope, 0, ActionExecutionStatus.Failed, "Attempt 1 failed");
        envelope.RecordAttemptFailure("Attempt 1 failed");

        AddExecuteAttemptLog(envelope, 1, ActionExecutionStatus.Failed, "Attempt 2 failed");
        envelope.RecordAttemptFailure("Attempt 2 failed");

        AddExecuteAttemptLog(envelope, 2, ActionExecutionStatus.Failed, "Attempt 3 failed");
        envelope.RecordAttemptFailure("Attempt 3 failed");
        
        _repository.AddEnvelope(envelope);

        var services = new ServiceCollection();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerFailure>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert
        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.DeadLettered));
        Assert.That(envelope.CompletedAt, Is.Not.Null);
        Assert.That(envelope.ExecutionLogs.Any(log => log.ActionType == ActionExecutionLogType.DeadLetter), Is.True);
    }

    [Test]
    public async Task OrchestrateAsync_ExactTypeOnly_NoPolymorphicMatching()
    {
        // Arrange
         var envelopeId = Id<CommandEnvelope>.New();
        var derivedCommand = new DerivedTestCommand { Value = "derived", Extra = 42 };
        var envelope = CreateEnvelope(envelopeId, derivedCommand);
        _repository.AddEnvelope(envelope);

        var services = new ServiceCollection();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler1>(); // Base type handler
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert - No handlers should match, zero-handler path
        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.Completed));
        Assert.That(envelope.CompletedAt, Is.Not.Null);
    }

    [Test]
    public async Task OrchestrateAsync_ZeroHandlers_CompletesWithWarning()
    {
        // Arrange
         var envelopeId = Id<CommandEnvelope>.New();
        var command = new TestCommand { Value = "test" };
        var envelope = CreateEnvelope(envelopeId, command);
        _repository.AddEnvelope(envelope);

        var services = new ServiceCollection();
        // No handlers registered
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert
        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.Completed));
        Assert.That(envelope.CompletedAt, Is.Not.Null);
    }

    [Test]
    public async Task OrchestrateAsync_EnvelopeNotFound_Skips()
    {
        // Arrange
         var envelopeId = Id<CommandEnvelope>.New();

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert
        Assert.That(_unitOfWork.SaveCallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task OrchestrateAsync_AlreadyCompleted_SkipsDuplicate()
    {
        // Arrange
         var envelopeId = Id<CommandEnvelope>.New();
        var command = new TestCommand { Value = "test" };
        var envelope = CreateEnvelope(envelopeId, command);
        envelope.MarkCompleted();
        _repository.AddEnvelope(envelope);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();
        var initialUpdateCount = _repository.UpdateCallCount;

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert - Should not update envelope after initial load
        Assert.That(_repository.UpdateCallCount, Is.EqualTo(initialUpdateCount));
    }

    [Test]
    public async Task OrchestrateAsync_InvalidCommandType_MarksDeadLettered()
    {
        // Arrange
         var envelopeId = Id<CommandEnvelope>.New();
        var envelope = new CommandEnvelope
        {
            Id = (LgymApi.Domain.ValueObjects.Id<CommandEnvelope>)envelopeId,
            CorrelationId = Id<CorrelationScope>.New(),
            PayloadJson = "{}",
            CommandTypeFullName = "NonExistent.FakeCommand",
            Status = ActionExecutionStatus.Pending
        };
        _repository.AddEnvelope(envelope);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert
        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.DeadLettered));
    }

    [Test]
    public async Task OrchestrateAsync_InvalidPayloadJson_MarksDeadLettered()
    {
        // Arrange
         var envelopeId = Id<CommandEnvelope>.New();
        var envelope = new CommandEnvelope
        {
            Id = (LgymApi.Domain.ValueObjects.Id<CommandEnvelope>)envelopeId,
            CorrelationId = Id<CorrelationScope>.New(),
            PayloadJson = "{invalid json",
            CommandTypeFullName = typeof(TestCommand).AssemblyQualifiedName!,
            Status = ActionExecutionStatus.Pending
        };
        _repository.AddEnvelope(envelope);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert
        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.DeadLettered));
    }
    [Test]
    public async Task OrchestrateAsync_ScopeIsolation_EachHandlerGetsDistinctScopedDependency()
    {
        // Arrange
         var envelopeId = Id<CommandEnvelope>.New();
        var command = new TestCommand { Value = "scope-test" };
        var envelope = CreateEnvelope(envelopeId, command);
        _repository.AddEnvelope(envelope);

        var capturedScopeIds = new System.Collections.Concurrent.ConcurrentBag<string>();

        var services = new ServiceCollection();
        services.AddScoped<ScopedTracker>(); // Scoped service to track instance isolation
        services.AddScoped<IBackgroundAction<TestCommand>>(sp => new ScopeAwareHandler(sp.GetRequiredService<ScopedTracker>(), capturedScopeIds));
        services.AddScoped<IBackgroundAction<TestCommand>>(sp => new ScopeAwareHandler(sp.GetRequiredService<ScopedTracker>(), capturedScopeIds));
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert - Each handler should have seen a distinct ScopedTracker instance
        // Assert - Each handler execution should use distinct scoped instance
        // Note: Handlers may be resolved multiple times (count check + execution), so we verify distinct instances >= 2
        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.Completed));
        Assert.That(capturedScopeIds.Distinct().Count(), Is.GreaterThanOrEqualTo(2), "At least 2 distinct scoped instances should exist (one per handler in isolated scope)");
    }

    [Test]
    public async Task OrchestrateAsync_ParallelismLimit_MaxFourConcurrentHandlers()
    {
        // Arrange
         var envelopeId = Id<CommandEnvelope>.New();
        var command = new TestCommand { Value = "concurrency-test" };
        var envelope = CreateEnvelope(envelopeId, command);
        _repository.AddEnvelope(envelope);

        var concurrencyTracker = new ConcurrencyTracker();

        var services = new ServiceCollection();
        // Register 8 handlers to exceed MaxDegreeOfParallelism=4
        for (int i = 0; i < 8; i++)
        {
            services.AddScoped<IBackgroundAction<TestCommand>>(_ => new ConcurrencyTestHandler(concurrencyTracker));
        }
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert - Max concurrent handlers never exceeds 4
        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.Completed));
        Assert.That(concurrencyTracker.MaxConcurrentExecutions, Is.LessThanOrEqualTo(4),
            "MaxDegreeOfParallelism=4 should be enforced");
        Assert.That(concurrencyTracker.TotalExecutions, Is.EqualTo(8), "All 8 handlers should execute");
    }

    [Test]
    public async Task OrchestrateAsync_PerHandlerErrorTracking_DurableExecutionLogs()
    {
        // Arrange
         var envelopeId = Id<CommandEnvelope>.New();
        var command = new TestCommand { Value = "error-tracking" };
        var envelope = CreateEnvelope(envelopeId, command);
        _repository.AddEnvelope(envelope);

        var services = new ServiceCollection();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerSuccess>();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerFailure>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act & Assert - Should throw because one handler failed (retry trigger)
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await orchestrator.OrchestrateAsync((Id<CommandEnvelope>)envelopeId));
        
        // Assert - Each handler execution should be tracked in ExecutionLogs
        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        var handlerLogs = envelope.ExecutionLogs.Where(log => log.ActionType == ActionExecutionLogType.HandlerExecution).ToList();
        Assert.That(handlerLogs.Count, Is.EqualTo(2), "Both handlers should have ExecutionLog entries");
        Assert.That(handlerLogs.Count(log => log.Status == ActionExecutionStatus.Completed), Is.EqualTo(1), "One handler succeeded");
        Assert.That(handlerLogs.Count(log => log.Status == ActionExecutionStatus.Failed), Is.EqualTo(1), "One handler failed");
        Assert.That(handlerLogs.All(log => !string.IsNullOrWhiteSpace(log.HandlerTypeName)), Is.True, "Each handler log should include concrete handler type");
        Assert.That(handlerLogs.Single(log => log.Status == ActionExecutionStatus.Failed).ErrorMessage,
            Is.Not.Null.And.Not.Empty, "Failed handler should have error message");
    }


    [Test]
    public async Task OrchestrateAsync_FullNameTypeResolution_ResolvesWithoutAssemblyQualifiedName()
    {
        // Arrange: Use FullName only (no assembly qualification)
         var envelopeId = Id<CommandEnvelope>.New();
        var command = new TestCommand { Value = "test" };
        var envelope = CreateEnvelope(envelopeId, command);
        // Override with FullName only (Type.FullName instead of AssemblyQualifiedName)
        envelope.CommandTypeFullName = typeof(TestCommand).FullName!;
        _repository.AddEnvelope(envelope);

        var services = new ServiceCollection();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler1>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert: Should resolve successfully using CommandDescriptor.ResolveCommandType
        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.Completed),
            "FullName-only type resolution should succeed");
    }

    [Test]
    public async Task OrchestrateAsync_AllHandlersSucceed_CreatesPerHandlerExecutionLogs()
    {
        // Arrange
         var envelopeId = Id<CommandEnvelope>.New();
        var command = new TestCommand { Value = "test" };
        var envelope = CreateEnvelope(envelopeId, command);
        _repository.AddEnvelope(envelope);

        var services = new ServiceCollection();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler1>();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler2>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert: Verify per-handler ExecutionLog entries exist for successful execution
        var handlerLogs = envelope.ExecutionLogs.Where(log => log.ActionType == ActionExecutionLogType.HandlerExecution).ToList();
        Assert.That(handlerLogs.Count, Is.EqualTo(2), "Should create ExecutionLog for each handler");
        Assert.That(handlerLogs.All(log => log.Status == ActionExecutionStatus.Completed),
            Is.True, "All handler logs should show Completed status");
        Assert.That(handlerLogs.All(log => !string.IsNullOrWhiteSpace(log.HandlerTypeName)),
            Is.True, "Successful handler logs should include concrete handler type");
        Assert.That(handlerLogs.All(log => log.ErrorMessage == null),
            Is.True, "Successful handlers should have no error message");
    }

    [Test]
    public async Task OrchestrateAsync_RetryableFailure_ThrowsExceptionToTriggerHangfireRetry()
    {
        // Arrange
         var envelopeId = Id<CommandEnvelope>.New();
        var command = new TestCommand { Value = "test" };
        var envelope = CreateEnvelope(envelopeId, command);
        _repository.AddEnvelope(envelope);

        var services = new ServiceCollection();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerFailure>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act & Assert - Should throw to trigger Hangfire AutomaticRetry
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await orchestrator.OrchestrateAsync((Id<CommandEnvelope>)envelopeId));
        
        Assert.That(ex!.Message, Does.Contain("Retry scheduled"), "Exception message should indicate retry scheduled");
        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.Failed), "Should be in Failed state");
        Assert.That(envelope.NextAttemptAt, Is.Not.Null, "NextAttemptAt should be scheduled");
        Assert.That(envelope.NextAttemptAt.Value, Is.GreaterThan(DateTimeOffset.UtcNow), "NextAttemptAt should be in future (backoff delay)");
    }

    [Test]
    public async Task OrchestrateAsync_RetryableFailure_SecondAttemptAlsoThrows()
    {
        // Arrange
         var envelopeId = Id<CommandEnvelope>.New();
        var command = new TestCommand { Value = "test" };
        var envelope = CreateEnvelope(envelopeId, command);
        // Simulate first failure already recorded.
        AddExecuteAttemptLog(envelope, 0, ActionExecutionStatus.Failed, "First attempt failed");
        envelope.RecordAttemptFailure("First attempt failed");
        _repository.AddEnvelope(envelope);

        var services = new ServiceCollection();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerFailure>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act & Assert - Second attempt should also throw (ShouldRetry still true)
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await orchestrator.OrchestrateAsync((Id<CommandEnvelope>)envelopeId));
        
        Assert.That(ex!.Message, Does.Contain("Retry scheduled"), "Exception should indicate retry");
        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(envelope.ExecutionLogs.Count(log => log.ActionType == ActionExecutionLogType.Execute), Is.EqualTo(2), "Should have 2 execution attempts");
    }

    [Test]
    public async Task OrchestrateAsync_AlreadyProcessing_SkipsDuplicateRedelivery()
    {
        // Arrange
        var envelopeId = Id<CommandEnvelope>.New();
        var command = new TestCommand { Value = "duplicate-redelivery" };
        var envelope = CreateEnvelope(envelopeId, command);
        envelope.Status = ActionExecutionStatus.Processing;
        envelope.LastAttemptAt = DateTimeOffset.UtcNow;
        _repository.AddEnvelope(envelope);

        var services = new ServiceCollection();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerFailure>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
        await orchestrator.OrchestrateAsync(envelopeId);

        // Assert
        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.Processing));
        Assert.That(envelope.ExecutionLogs, Is.Empty);
        Assert.That(_repository.UpdateCallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task OrchestrateAsync_ErrorContextDurability_StoresFullExceptionStackTrace()
    {
        // Arrange
         var envelopeId = Id<CommandEnvelope>.New();
        var command = new TestCommand { Value = "error-context" };
        var envelope = CreateEnvelope(envelopeId, command);
        _repository.AddEnvelope(envelope);

        var services = new ServiceCollection();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerFailure>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act & Assert - Should throw to trigger retry
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await orchestrator.OrchestrateAsync((Id<CommandEnvelope>)envelopeId));
        
        // Assert - ExecutionLog should contain full exception details
        var handlerLogs = envelope.ExecutionLogs.Where(log => log.ActionType == ActionExecutionLogType.HandlerExecution).ToList();
        Assert.That(handlerLogs.Count, Is.EqualTo(1), "Should have one handler execution log");
        
        var failedLog = handlerLogs.Single();
        Assert.That(failedLog.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(failedLog.ErrorMessage, Is.Not.Null.And.Not.Empty, "Should have error message");
        Assert.That(failedLog.ErrorDetails, Is.Not.Null.And.Not.Empty, "Should have error details");
        Assert.That(failedLog.ErrorDetails, Does.Contain("InvalidOperationException"), "Should contain exception type");
        Assert.That(failedLog.ErrorDetails, Does.Contain("Test handler failure"), "Should contain exception message");
        Assert.That(failedLog.ErrorDetails, Does.Contain("at "), "Should contain stack trace");
    }

    private BackgroundActionOrchestratorService CreateOrchestrator()
    {
        return new BackgroundActionOrchestratorService(
            _serviceProvider,
            _repository,
            _unitOfWork,
            new FakeLogger());
    }

    private static CommandEnvelope CreateEnvelope<TCommand>(Id<CommandEnvelope> envelopeId, TCommand command)
        where TCommand : IActionCommand
    {
        return new CommandEnvelope
        {
            Id = envelopeId,
            CorrelationId = Id<CorrelationScope>.New(),
            PayloadJson = JsonSerializer.Serialize(command, SharedSerializationOptions.Current),
            CommandTypeFullName = typeof(TCommand).AssemblyQualifiedName!,
            Status = ActionExecutionStatus.Pending
        };
    }

    private static void AddExecuteAttemptLog(CommandEnvelope envelope, int attemptNumber, ActionExecutionStatus status, string? errorMessage = null)
    {
        envelope.ExecutionLogs.Add(new ActionExecutionLog
        {
            CommandEnvelopeId = envelope.Id,
            ActionType = ActionExecutionLogType.Execute,
            Status = status,
            AttemptNumber = attemptNumber,
            ErrorMessage = errorMessage
        });
    }

    // Test command and handlers
    private class TestCommand : IActionCommand
    {
        public string Value { get; set; } = string.Empty;
    }

    private class DerivedTestCommand : TestCommand
    {
        public int Extra { get; set; }
    }

    private class TestActionHandler1 : IBackgroundAction<TestCommand>
    {
        public Task ExecuteAsync(TestCommand command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private class TestActionHandler2 : IBackgroundAction<TestCommand>
    {
        public Task ExecuteAsync(TestCommand command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private class TestActionHandlerSuccess : IBackgroundAction<TestCommand>
    {
        public Task ExecuteAsync(TestCommand command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private class TestActionHandlerFailure : IBackgroundAction<TestCommand>
    {
        public Task ExecuteAsync(TestCommand command, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Test handler failure");
        }
    }

    // Test helpers for scope isolation and concurrency verification

    private sealed class ScopedTracker
    {
        public string InstanceId { get; } = $"{Id<ScopedTracker>.New():N}";
    }

    private sealed class ConcurrencyTracker
    {
        private int _currentConcurrent = 0;
        private int _maxConcurrent = 0;
        private int _totalExecutions = 0;
        private readonly object _lock = new object();

        public int MaxConcurrentExecutions => _maxConcurrent;
        public int TotalExecutions => _totalExecutions;

        public async Task TrackExecutionAsync()
        {
            lock (_lock)
            {
                _currentConcurrent++;
                _totalExecutions++;
                if (_currentConcurrent > _maxConcurrent)
                {
                    _maxConcurrent = _currentConcurrent;
                }
            }

            // Simulate work to increase concurrency pressure
            await Task.Delay(50);

            lock (_lock)
            {
                _currentConcurrent--;
            }
        }
    }


    private sealed class ScopeAwareHandler : IBackgroundAction<TestCommand>
    {
        private readonly ScopedTracker _tracker;
        private readonly System.Collections.Concurrent.ConcurrentBag<string> _capturedIds;

        public ScopeAwareHandler(ScopedTracker tracker, System.Collections.Concurrent.ConcurrentBag<string> capturedIds)
        {
            _tracker = tracker;
            _capturedIds = capturedIds;
            _capturedIds.Add(_tracker.InstanceId);
        }

        public Task ExecuteAsync(TestCommand command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
    private sealed class ConcurrencyTestHandler : IBackgroundAction<TestCommand>
    {
        private readonly ConcurrencyTracker _tracker;

        public ConcurrencyTestHandler(ConcurrencyTracker tracker)
        {
            _tracker = tracker;
        }

        public async Task ExecuteAsync(TestCommand command, CancellationToken cancellationToken = default)
        {
            await _tracker.TrackExecutionAsync();
        }
    }

    // Fake implementations for testing
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

    private sealed class FakeTransaction : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeLogger : ILogger<BackgroundActionOrchestratorService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
