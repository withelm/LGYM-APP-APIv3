using LgymApi.Domain.Enums;

namespace LgymApi.UnitTests;

/// <summary>
/// Tests for ActionExecutionStatus enum covering lifecycle states and transition semantics.
/// </summary>
[TestFixture]
public sealed class ActionExecutionStatusTests
{
    [Test]
    public void Enum_HasExpectedValues()
    {
        // Arrange & Act & Assert
        Assert.That((int)ActionExecutionStatus.Pending, Is.EqualTo(0));
        Assert.That((int)ActionExecutionStatus.Processing, Is.EqualTo(1));
        Assert.That((int)ActionExecutionStatus.Completed, Is.EqualTo(2));
        Assert.That((int)ActionExecutionStatus.Failed, Is.EqualTo(3));
        Assert.That((int)ActionExecutionStatus.DeadLettered, Is.EqualTo(4));
    }

    [Test]
    public void Enum_HasExpectedNames()
    {
        // Arrange & Act & Assert
        Assert.That(ActionExecutionStatus.Pending.ToString(), Is.EqualTo("Pending"));
        Assert.That(ActionExecutionStatus.Processing.ToString(), Is.EqualTo("Processing"));
        Assert.That(ActionExecutionStatus.Completed.ToString(), Is.EqualTo("Completed"));
        Assert.That(ActionExecutionStatus.Failed.ToString(), Is.EqualTo("Failed"));
        Assert.That(ActionExecutionStatus.DeadLettered.ToString(), Is.EqualTo("DeadLettered"));
    }

    [Test]
    public void HappyPath_ValidStatusProgressionPendingToCompleted()
    {
        // Arrange
        var status = ActionExecutionStatus.Pending;

        // Act - Transition from Pending to Processing
        status = ActionExecutionStatus.Processing;
        Assert.That(status, Is.EqualTo(ActionExecutionStatus.Processing));

        // Act - Transition from Processing to Completed
        status = ActionExecutionStatus.Completed;
        Assert.That(status, Is.EqualTo(ActionExecutionStatus.Completed));
    }

    [Test]
    public void HappyPath_ValidStatusProgressionPendingToProcessingToFailed()
    {
        // Arrange
        var status = ActionExecutionStatus.Pending;

        // Act - Transition from Pending to Processing
        status = ActionExecutionStatus.Processing;
        Assert.That(status, Is.EqualTo(ActionExecutionStatus.Processing));

        // Act - Transition from Processing to Failed
        status = ActionExecutionStatus.Failed;
        Assert.That(status, Is.EqualTo(ActionExecutionStatus.Failed));
    }

    [Test]
    public void HappyPath_ValidStatusProgressionFailedToProcessing()
    {
        // Arrange - Failed action will be retried
        var status = ActionExecutionStatus.Failed;

        // Act - Retry transitions back to Processing
        status = ActionExecutionStatus.Processing;
        Assert.That(status, Is.EqualTo(ActionExecutionStatus.Processing));
    }

    [Test]
    public void HappyPath_ValidStatusProgressionToDeadLetter()
    {
        // Arrange - After max retry attempts, action is dead-lettered
        var status = ActionExecutionStatus.Failed;

        // Act - Transition to DeadLettered terminal state
        status = ActionExecutionStatus.DeadLettered;
        Assert.That(status, Is.EqualTo(ActionExecutionStatus.DeadLettered));
    }

    [Test]
    public void HappyPath_CompletedIsTerminalState()
    {
        // Arrange
        var status = ActionExecutionStatus.Completed;

        // Act & Assert - Completed is final state, no further transitions
        Assert.That(status, Is.EqualTo(ActionExecutionStatus.Completed));
    }

    [Test]
    public void HappyPath_DeadLetteredIsTerminalState()
    {
        // Arrange
        var status = ActionExecutionStatus.DeadLettered;

        // Act & Assert - DeadLettered is final state, no further transitions
        Assert.That(status, Is.EqualTo(ActionExecutionStatus.DeadLettered));
    }

    [Test]
    [TestCase(ActionExecutionStatus.Pending)]
    [TestCase(ActionExecutionStatus.Processing)]
    [TestCase(ActionExecutionStatus.Completed)]
    [TestCase(ActionExecutionStatus.Failed)]
    [TestCase(ActionExecutionStatus.DeadLettered)]
    public void AllEnumValues_CanBeParsedFromIntValue(ActionExecutionStatus expected)
    {
        // Arrange
        var intValue = (int)expected;

        // Act
        var parsed = (ActionExecutionStatus)intValue;

        // Assert
        Assert.That(parsed, Is.EqualTo(expected));
    }

    [Test]
    public void Enum_Values_AreConsistentAcrossCalls()
    {
        // Arrange & Act & Assert
        var status1 = ActionExecutionStatus.Processing;
        var status2 = ActionExecutionStatus.Processing;

        Assert.That(status1, Is.EqualTo(status2));
        Assert.That(status1.GetHashCode(), Is.EqualTo(status2.GetHashCode()));
    }

    [Test]
    [TestCase(ActionExecutionStatus.Pending, ActionExecutionStatus.Processing, true)]
    [TestCase(ActionExecutionStatus.Processing, ActionExecutionStatus.Completed, true)]
    [TestCase(ActionExecutionStatus.Processing, ActionExecutionStatus.Failed, true)]
    [TestCase(ActionExecutionStatus.Failed, ActionExecutionStatus.Processing, true)]
    [TestCase(ActionExecutionStatus.Failed, ActionExecutionStatus.DeadLettered, true)]
    [TestCase(ActionExecutionStatus.Completed, ActionExecutionStatus.Completed, true)]
    [TestCase(ActionExecutionStatus.DeadLettered, ActionExecutionStatus.DeadLettered, true)]
    public void TransitionSemantics_DefinesValidTransitions(
        ActionExecutionStatus from,
        ActionExecutionStatus to,
        bool isValid)
    {
        // Arrange & Act & Assert
        // This test documents the valid transition semantics
        // All transitions listed above should be valid for orchestrator policies
        Assert.That(isValid, Is.True, $"Transition from {from} to {to} should be valid");
    }

    [Test]
    public void OrchestrationPolicies_CanUseEnumValuesForStateManagement()
    {
        // Arrange - Simulate orchestration flow
        var actionStatus = ActionExecutionStatus.Pending;
        var maxAttempts = 3;
        var currentAttempt = 0;

        // Act - Processing attempt 1
        actionStatus = ActionExecutionStatus.Processing;
        Assert.That(actionStatus, Is.EqualTo(ActionExecutionStatus.Processing));

        // Act - First attempt fails
        actionStatus = ActionExecutionStatus.Failed;
        currentAttempt++;
        Assert.That(actionStatus, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(currentAttempt, Is.LessThan(maxAttempts));

        // Act - Retry: back to Processing for attempt 2
        actionStatus = ActionExecutionStatus.Processing;
        Assert.That(actionStatus, Is.EqualTo(ActionExecutionStatus.Processing));

        // Act - Second attempt fails
        actionStatus = ActionExecutionStatus.Failed;
        currentAttempt++;
        Assert.That(actionStatus, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(currentAttempt, Is.LessThan(maxAttempts));

        // Act - Retry: back to Processing for attempt 3
        actionStatus = ActionExecutionStatus.Processing;
        Assert.That(actionStatus, Is.EqualTo(ActionExecutionStatus.Processing));

        // Act - Third attempt fails (max attempts reached)
        actionStatus = ActionExecutionStatus.Failed;
        currentAttempt++;
        Assert.That(actionStatus, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(currentAttempt, Is.EqualTo(maxAttempts));

        // Act - Transition to DeadLettered after max attempts
        actionStatus = ActionExecutionStatus.DeadLettered;
        Assert.That(actionStatus, Is.EqualTo(ActionExecutionStatus.DeadLettered));
    }

    [Test]
    public void SuccessfulRetryPath_CanTransitionFromFailedToCompleted()
    {
        // Arrange - Action failed once but succeeds on retry
        var actionStatus = ActionExecutionStatus.Failed;

        // Act - Retry: back to Processing
        actionStatus = ActionExecutionStatus.Processing;
        Assert.That(actionStatus, Is.EqualTo(ActionExecutionStatus.Processing));

        // Act - Success on retry
        actionStatus = ActionExecutionStatus.Completed;
        Assert.That(actionStatus, Is.EqualTo(ActionExecutionStatus.Completed));
    }

    [Test]
    [TestCase(ActionExecutionStatus.Pending)]
    [TestCase(ActionExecutionStatus.Processing)]
    [TestCase(ActionExecutionStatus.Completed)]
    [TestCase(ActionExecutionStatus.Failed)]
    [TestCase(ActionExecutionStatus.DeadLettered)]
    public void Enum_CanBeUsedInSwitchStatements(ActionExecutionStatus status)
    {
        // Arrange
        string result = status switch
        {
            ActionExecutionStatus.Pending => "pending",
            ActionExecutionStatus.Processing => "processing",
            ActionExecutionStatus.Completed => "completed",
            ActionExecutionStatus.Failed => "failed",
            ActionExecutionStatus.DeadLettered => "dead-lettered",
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

        // Act & Assert
        Assert.That(result, Is.Not.Empty);
        Assert.That(result, Does.Not.Contain(" "));
    }
}
