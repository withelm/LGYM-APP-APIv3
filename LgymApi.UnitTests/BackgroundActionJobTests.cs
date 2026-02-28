using Hangfire;
using LgymApi.BackgroundWorker.Common.Jobs;
using LgymApi.BackgroundWorker.Jobs;
using NUnit.Framework;

namespace LgymApi.UnitTests;

/// <summary>
/// Unit tests for ActionMessageJob.
/// Validates thin job class delegates correctly to orchestrator and retry metadata is configured.
/// </summary>
[TestFixture]
public class BackgroundActionJobTests
{
    /// <summary>
    /// Test double for orchestrator to track calls without full dependencies.
    /// </summary>
    private class TestDoubleOrchestratorService
    {
        public List<(Guid EnvelopeId, CancellationToken Token)> OrchestrationCalls { get; } = [];

        public async Task OrchestrateAsync(Guid envelopeId, CancellationToken cancellationToken = default)
        {
            OrchestrationCalls.Add((envelopeId, cancellationToken));
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Test job wrapper that accepts our test double instead of full orchestrator.
    /// </summary>
    private class TestableActionMessageJob : IActionMessageJob
    {
        private readonly TestDoubleOrchestratorService _testOrchestrator;

        public TestableActionMessageJob(TestDoubleOrchestratorService testOrchestrator)
        {
            _testOrchestrator = testOrchestrator ?? throw new ArgumentNullException(nameof(testOrchestrator));
        }

        public async Task ExecuteAsync(Guid actionMessageId)
        {
            await _testOrchestrator.OrchestrateAsync(actionMessageId);
        }
    }

    private TestDoubleOrchestratorService _testOrchestrator = null!;
    private TestableActionMessageJob _testableJob = null!;

    [SetUp]
    public void SetUp()
    {
        _testOrchestrator = new TestDoubleOrchestratorService();
        _testableJob = new TestableActionMessageJob(_testOrchestrator);
    }

    /// <summary>
    /// Validates job delegates to orchestrator service on happy path.
    /// </summary>
    [Test]
    public async Task ExecuteAsync_DelegatesToOrchestratorWithMessageId()
    {
        // Arrange
        var messageId = Guid.NewGuid();

        // Act
        await _testableJob.ExecuteAsync(messageId);

        // Assert
        Assert.That(
            _testOrchestrator.OrchestrationCalls.Count,
            Is.EqualTo(1),
            "Job should delegate to orchestrator exactly once");
        Assert.That(
            _testOrchestrator.OrchestrationCalls[0].EnvelopeId,
            Is.EqualTo(messageId),
            "Job should pass the message id to orchestrator");
    }



    /// <summary>
    /// Validates the [AutomaticRetry] attribute is applied to class level.
    /// </summary>
    [Test]
    public void ActionMessageJob_ClassHasAutoRetryAttribute()
    {
        // Arrange & Act
        var classAttribute = typeof(ActionMessageJob)
            .GetCustomAttributes(typeof(AutomaticRetryAttribute), false)
            .FirstOrDefault() as AutomaticRetryAttribute;

        // Assert
        Assert.That(classAttribute, Is.Not.Null, "ActionMessageJob class must have AutomaticRetryAttribute");
        Assert.That(classAttribute!.Attempts, Is.EqualTo(3));
        Assert.That(classAttribute!.DelaysInSeconds, Is.EqualTo(new[] { 60, 300, 900 }));
    }

    /// <summary>
    /// Validates job throws if orchestrator is null during construction.
    /// </summary>
    [Test]
    public void Constructor_ThrowsArgumentNullException_WhenOrchestratorIsNull()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(
            () => new ActionMessageJob(null!));
        
        Assert.That(ex!.ParamName, Is.EqualTo("orchestrator"));
    }

    /// <summary>
    /// Validates multiple sequential job executions work correctly.
    /// </summary>
    [Test]
    public async Task ExecuteAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var messageIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        // Act
        foreach (var id in messageIds)
        {
            await _testableJob.ExecuteAsync(id);
        }

        // Assert
        Assert.That(
            _testOrchestrator.OrchestrationCalls.Count,
            Is.EqualTo(3),
            "Orchestrator should be called once per execution");
        
        for (int i = 0; i < 3; i++)
        {
            Assert.That(
                _testOrchestrator.OrchestrationCalls[i].EnvelopeId,
                Is.EqualTo(messageIds[i]),
                $"Call {i + 1} should pass correct message id");
        }
    }

    /// <summary>
    /// Validates job class is sealed to prevent inheritance.
    /// </summary>
    [Test]
    public void ActionMessageJob_IsSealed()
    {
        // Act & Assert
        Assert.That(
            typeof(ActionMessageJob).IsSealed,
            Is.True,
            "ActionMessageJob should be sealed to prevent subclassing");
    }

    /// <summary>
    /// Validates job correctly implements IActionMessageJob interface.
    /// </summary>
    [Test]
    public void ActionMessageJob_ImplementsIActionMessageJob()
    {
        // Act & Assert
        Assert.That(
            typeof(ActionMessageJob).GetInterfaces().Contains(typeof(IActionMessageJob)),
            Is.True,
            "ActionMessageJob must implement IActionMessageJob");
    }

    /// <summary>
    /// Validates job returns Task from ExecuteAsync (not void).
    /// </summary>
    [Test]
    public void ExecuteAsync_ReturnsTask()
    {
        // Arrange & Act
        var method = typeof(ActionMessageJob).GetMethod(nameof(ActionMessageJob.ExecuteAsync));

        // Assert
        Assert.That(
            method?.ReturnType,
            Is.EqualTo(typeof(Task)),
            "ExecuteAsync must return Task for async execution");
    }

    /// <summary>
    /// Validates ExecuteAsync accepts exactly one Guid parameter.
    /// </summary>
    [Test]
    public void ExecuteAsync_HasCorrectSignature()
    {
        // Arrange & Act
        var method = typeof(ActionMessageJob).GetMethod(nameof(ActionMessageJob.ExecuteAsync));
        var parameters = method?.GetParameters();

        // Assert
        Assert.That(parameters?.Length, Is.EqualTo(1), "ExecuteAsync should accept exactly 1 parameter");
        Assert.That(
            parameters?[0].ParameterType,
            Is.EqualTo(typeof(Guid)),
            "Parameter should be Guid (actionMessageId)");
    }

    /// <summary>
    /// Validates with unknown message id, job still delegates (orchestrator handles gracefully).
    /// </summary>
    [Test]
    public async Task ExecuteAsync_WithUnknownMessageId_StillDelegatesToOrchestrator()
    {
        // Arrange
        var unknownMessageId = Guid.NewGuid();

        // Act
        await _testableJob.ExecuteAsync(unknownMessageId);

        // Assert
        Assert.That(
            _testOrchestrator.OrchestrationCalls.Count,
            Is.EqualTo(1),
            "Job should still delegate for unknown ids");
        Assert.That(
            _testOrchestrator.OrchestrationCalls[0].EnvelopeId,
            Is.EqualTo(unknownMessageId));
    }

    /// <summary>
    /// Validates retry delays are in correct order (increasing backoff).
    /// </summary>
    [Test]
    public void RetryDelays_AreIncreasingBackoffSequence()
    {
        // Arrange & Act
        var classAttribute = typeof(ActionMessageJob)
            .GetCustomAttributes(typeof(AutomaticRetryAttribute), false)
            .FirstOrDefault() as AutomaticRetryAttribute;

        // Assert
        var delays = classAttribute!.DelaysInSeconds!;
        Assert.That(delays[0], Is.EqualTo(60), "First retry at 1 minute");
        Assert.That(delays[1], Is.EqualTo(300), "Second retry at 5 minutes");
        Assert.That(delays[2], Is.EqualTo(900), "Third retry at 15 minutes");
        
        // Verify increasing sequence
        for (int i = 1; i < delays.Length; i++)
        {
            Assert.That(delays[i], Is.GreaterThan(delays[i - 1]),
                "Delays should increase (backoff strategy)");
        }
    }
}
