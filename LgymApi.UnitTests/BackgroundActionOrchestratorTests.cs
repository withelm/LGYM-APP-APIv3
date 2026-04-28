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
using FluentAssertions;

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
        services.AddScoped<IUnitOfWork>(_ => _unitOfWork);
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler1>();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler2>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert
        envelope.Status.Should().Be(ActionExecutionStatus.Completed);
        envelope.CompletedAt.Should().NotBeNull();
        _unitOfWork.SaveCallCount.Should().BeGreaterThanOrEqualTo(2);
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
        services.AddScoped<IUnitOfWork>(_ => _unitOfWork);
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerSuccess>();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerFailure>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();
        var startedAt = DateTimeOffset.UtcNow;

         // Act & Assert - Should throw because one handler failed
         var ex = await FluentActions.Invoking(async () =>
             await orchestrator.OrchestrateAsync((Id<CommandEnvelope>)envelopeId)).Should().ThrowAsync<InvalidOperationException>();
         
         // Assert - Failure recorded and exception thrown for Hangfire retry
         ex.Which.Message.Should().Contain("Retry scheduled");
          envelope.Status.Should().Be(ActionExecutionStatus.Failed);
          envelope.ExecutionLogs.Count.Should().BeGreaterThanOrEqualTo(1);
          envelope.NextAttemptAt.Should().NotBeNull();
          (envelope.NextAttemptAt!.Value - startedAt).Should().BeCloseTo(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(2));
          // First failed orchestration should schedule first retry delay even with multiple handlers.
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
        services.AddScoped<IUnitOfWork>(_ => _unitOfWork);
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerFailure>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

         // Act & Assert
         await FluentActions.Invoking(async () =>
             await orchestrator.OrchestrateAsync((Id<CommandEnvelope>)envelopeId)).Should().ThrowAsync<InvalidOperationException>();
         
         // Assert - Failure recorded, retry scheduled, exception thrown
         envelope.Status.Should().Be(ActionExecutionStatus.Failed);
         envelope.LastAttemptAt.Should().NotBeNull();
         envelope.NextAttemptAt.Should().NotBeNull();
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
        services.AddScoped<IUnitOfWork>(_ => _unitOfWork);
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerFailure>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert
        envelope.Status.Should().Be(ActionExecutionStatus.DeadLettered);
        envelope.CompletedAt.Should().NotBeNull();
        envelope.ExecutionLogs.Any(log => log.ActionType == ActionExecutionLogType.DeadLetter).Should().BeTrue();
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
        services.AddScoped<IUnitOfWork>(_ => _unitOfWork);
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler1>(); // Base type handler
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert - No handlers should match, zero-handler path
        envelope.Status.Should().Be(ActionExecutionStatus.Completed);
        envelope.CompletedAt.Should().NotBeNull();
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
        services.AddScoped<IUnitOfWork>(_ => _unitOfWork);
        // No handlers registered
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert
        envelope.Status.Should().Be(ActionExecutionStatus.Completed);
        envelope.CompletedAt.Should().NotBeNull();
    }

    [Test]
    public async Task OrchestrateAsync_EnvelopeNotFound_Skips()
    {
        // Arrange
         var envelopeId = Id<CommandEnvelope>.New();

        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => _unitOfWork);
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert
        _unitOfWork.SaveCallCount.Should().Be(0);
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
        services.AddScoped<IUnitOfWork>(_ => _unitOfWork);
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();
        var initialUpdateCount = _repository.UpdateCallCount;

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert - Should not update envelope after initial load
        _repository.UpdateCallCount.Should().Be(initialUpdateCount);
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
        services.AddScoped<IUnitOfWork>(_ => _unitOfWork);
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert
        envelope.Status.Should().Be(ActionExecutionStatus.DeadLettered);
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
        services.AddScoped<IUnitOfWork>(_ => _unitOfWork);
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert
        envelope.Status.Should().Be(ActionExecutionStatus.DeadLettered);
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
        services.AddScoped<IUnitOfWork>(_ => _unitOfWork);
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
        envelope.Status.Should().Be(ActionExecutionStatus.Completed);
        capturedScopeIds.Distinct().Count().Should().BeGreaterThanOrEqualTo(2, "At least 2 distinct scoped instances should exist (one per handler in isolated scope)");
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
        services.AddScoped<IUnitOfWork>(_ => _unitOfWork);
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
        envelope.Status.Should().Be(ActionExecutionStatus.Completed);
        concurrencyTracker.MaxConcurrentExecutions.Should().BeLessThanOrEqualTo(4, "MaxDegreeOfParallelism=4 should be enforced");
        concurrencyTracker.TotalExecutions.Should().Be(8, "All 8 handlers should execute");
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
        services.AddScoped<IUnitOfWork>(_ => _unitOfWork);
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerSuccess>();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerFailure>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

         // Act & Assert - Should throw because one handler failed (retry trigger)
         await FluentActions.Invoking(async () =>
             await orchestrator.OrchestrateAsync((Id<CommandEnvelope>)envelopeId)).Should().ThrowAsync<InvalidOperationException>();
         
         // Assert - Each handler execution should be tracked in ExecutionLogs
         envelope.Status.Should().Be(ActionExecutionStatus.Failed);
         var handlerLogs = envelope.ExecutionLogs.Where(log => log.ActionType == ActionExecutionLogType.HandlerExecution).ToList();
         handlerLogs.Count.Should().Be(2, "Both handlers should have ExecutionLog entries");
         handlerLogs.Count(log => log.Status == ActionExecutionStatus.Completed).Should().Be(1, "One handler succeeded");
        handlerLogs.Count(log => log.Status == ActionExecutionStatus.Failed).Should().Be(1, "One handler failed");
        handlerLogs.All(log => !string.IsNullOrWhiteSpace(log.HandlerTypeName)).Should().BeTrue("Each handler log should include concrete handler type");
        handlerLogs.Single(log => log.Status == ActionExecutionStatus.Failed).ErrorMessage.Should().NotBeNullOrEmpty("Failed handler should have error message");
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
        services.AddScoped<IUnitOfWork>(_ => _unitOfWork);
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler1>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert: Should resolve successfully using CommandDescriptor.ResolveCommandType
        envelope.Status.Should().Be(ActionExecutionStatus.Completed, "FullName-only type resolution should succeed");
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
        services.AddScoped<IUnitOfWork>(_ => _unitOfWork);
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler1>();
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandler2>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
         await orchestrator.OrchestrateAsync(envelopeId);

        // Assert: Verify per-handler ExecutionLog entries exist for successful execution
        var handlerLogs = envelope.ExecutionLogs.Where(log => log.ActionType == ActionExecutionLogType.HandlerExecution).ToList();
        handlerLogs.Count.Should().Be(2, "Should create ExecutionLog for each handler");
        handlerLogs.All(log => log.Status == ActionExecutionStatus.Completed).Should().BeTrue("All handler logs should show Completed status");
        handlerLogs.All(log => !string.IsNullOrWhiteSpace(log.HandlerTypeName)).Should().BeTrue("Successful handler logs should include concrete handler type");
        handlerLogs.All(log => log.ErrorMessage == null).Should().BeTrue("Successful handlers should have no error message");
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
        services.AddScoped<IUnitOfWork>(_ => _unitOfWork);
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerFailure>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

         // Act & Assert - Should throw to trigger Hangfire AutomaticRetry
         var ex = await FluentActions.Invoking(async () =>
             await orchestrator.OrchestrateAsync((Id<CommandEnvelope>)envelopeId)).Should().ThrowAsync<InvalidOperationException>();
         
         ex.Which.Message.Should().Contain("Retry scheduled", "Exception message should indicate retry scheduled");
         envelope.Status.Should().Be(ActionExecutionStatus.Failed, "Should be in Failed state");
         envelope.NextAttemptAt.Should().NotBeNull("NextAttemptAt should be scheduled");
         envelope.NextAttemptAt.Value.Should().BeAfter(DateTimeOffset.UtcNow, "NextAttemptAt should be in future (backoff delay)");
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
        services.AddScoped<IUnitOfWork>(_ => _unitOfWork);
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerFailure>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

         // Act & Assert - Second attempt should also throw (ShouldRetry still true)
         var ex = await FluentActions.Invoking(async () =>
             await orchestrator.OrchestrateAsync((Id<CommandEnvelope>)envelopeId)).Should().ThrowAsync<InvalidOperationException>();
         
         ex.Which.Message.Should().Contain("Retry scheduled", "Exception should indicate retry");
         envelope.Status.Should().Be(ActionExecutionStatus.Failed);
         envelope.ExecutionLogs.Count(log => log.ActionType == ActionExecutionLogType.Execute).Should().Be(2, "Should have 2 execution attempts");
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
        services.AddScoped<IUnitOfWork>(_ => _unitOfWork);
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerFailure>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

        var orchestrator = CreateOrchestrator();

        // Act
        await orchestrator.OrchestrateAsync(envelopeId);

        // Assert
        envelope.Status.Should().Be(ActionExecutionStatus.Processing);
        envelope.ExecutionLogs.Should().BeEmpty();
        _repository.UpdateCallCount.Should().Be(0);
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
        services.AddScoped<IUnitOfWork>(_ => _unitOfWork);
        services.AddScoped<IBackgroundAction<TestCommand>, TestActionHandlerFailure>();
        services.AddSingleton<ILogger<BackgroundActionOrchestratorService>>(_ => new FakeLogger());
        _serviceProvider = services.BuildServiceProvider();

         var orchestrator = CreateOrchestrator();

         // Act & Assert - Should throw to trigger retry
         await FluentActions.Invoking(async () =>
             await orchestrator.OrchestrateAsync((Id<CommandEnvelope>)envelopeId)).Should().ThrowAsync<InvalidOperationException>();
         
         // Assert - ExecutionLog should contain full exception details
         var handlerLogs = envelope.ExecutionLogs.Where(log => log.ActionType == ActionExecutionLogType.HandlerExecution).ToList();
         handlerLogs.Count.Should().Be(1, "Should have one handler execution log");
         
        var failedLog = handlerLogs.Single();
        failedLog.Status.Should().Be(ActionExecutionStatus.Failed);
        failedLog.ErrorMessage.Should().NotBeNullOrEmpty("Should have error message");
        failedLog.ErrorDetails.Should().NotBeNullOrEmpty("Should have error details");
        failedLog.ErrorDetails.Should().Contain("InvalidOperationException", "Should contain exception type");
        failedLog.ErrorDetails.Should().Contain("Test handler failure", "Should contain exception message");
        failedLog.ErrorDetails.Should().Contain("at ", "Should contain stack trace");
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






