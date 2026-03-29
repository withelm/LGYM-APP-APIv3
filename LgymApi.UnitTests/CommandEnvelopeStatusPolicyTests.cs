using LgymApi.Domain.ValueObjects;
using NUnit.Framework;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using System;
using System.Linq;

namespace LgymApi.UnitTests;

/// <summary>
/// TDD tests for CommandEnvelope status and error history policy implementation.
/// Tests retry mechanics, dead-lettering, and error preservation across attempt sequences.
/// </summary>
[TestFixture]
public class CommandEnvelopeStatusPolicyTests
{
    private CommandEnvelope _envelope = null!;

    [SetUp]
    public void SetUp()
    {
        // Fresh envelope for each test, Pending status
        _envelope = new CommandEnvelope
        {
            Id = LgymApi.Domain.ValueObjects.Id<CommandEnvelope>.New(),
            CorrelationId = Id<CorrelationScope>.New(),
            PayloadJson = "{\"test\": true}",
            CommandTypeFullName = "TestNamespace.TestCommand",
            Status = ActionExecutionStatus.Pending
        };
    }

    #region Happy Path: Transient Failure -> Success

    [Test]
    public void RecordAttemptFailure_FirstAttempt_RecordsErrorAndSchedulesRetry()
    {
        // Arrange
        var errorMsg = "Timeout on first attempt";
        var errorDetails = "System.TimeoutException: The operation timed out";
        
        // Simulate orchestrator adding HandlerExecution log for first attempt
        AddMockHandlerExecutionLog(_envelope, 0, ActionExecutionStatus.Failed, errorMsg, errorDetails);

        // Act
        _envelope.RecordAttemptFailure(errorMsg, errorDetails);

        // Assert
        Assert.That(_envelope.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(_envelope.LastAttemptAt, Is.Not.Null);
        Assert.That(_envelope.NextAttemptAt, Is.Not.Null);

        // Verify orchestrator added the log (no logs created by envelope method itself)
        Assert.That(_envelope.ExecutionLogs.Count(log => log.ActionType == ActionExecutionLogType.HandlerExecution), Is.EqualTo(1));
    }

    [Test]
    public void RecordAttemptFailure_FirstAttempt_SchedulesCorrectBackoff()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        
        // Simulate orchestrator adding HandlerExecution log for first attempt
        AddMockHandlerExecutionLog(_envelope, 0, ActionExecutionStatus.Failed, "Attempt 1 failed");

        // Act
        _envelope.RecordAttemptFailure("Attempt 1 failed");
        var firstRetryTime = _envelope.NextAttemptAt;

        // Assert - should schedule 60 second backoff for first retry
        Assert.That(firstRetryTime, Is.Not.Null);
        var expectedDelay = TimeSpan.FromSeconds(60);
        var actualDelay = firstRetryTime!.Value - now;
        Assert.That(actualDelay, Is.GreaterThanOrEqualTo(expectedDelay - TimeSpan.FromMilliseconds(100)));
        Assert.That(actualDelay, Is.LessThanOrEqualTo(expectedDelay + TimeSpan.FromMilliseconds(100)));
    }

    [Test]
    public void RecordAttemptFailure_SecondAttempt_SchedulesLongerBackoff()
    {
        // Arrange - simulate first attempt
        AddMockHandlerExecutionLog(_envelope, 0, ActionExecutionStatus.Failed, "Attempt 1 failed");
        _envelope.RecordAttemptFailure("Attempt 1 failed");
        var firstRetryTime = _envelope.NextAttemptAt;

        // Simulate retry execution and second failure
        _envelope.Status = ActionExecutionStatus.Failed; // Would be retried by orchestrator
        var now = DateTimeOffset.UtcNow;
        AddMockHandlerExecutionLog(_envelope, 1, ActionExecutionStatus.Failed, "Attempt 2 failed");

        // Act
        _envelope.RecordAttemptFailure("Attempt 2 failed");

        // Assert - should schedule 300 second (5m) backoff for second retry
        var secondRetryTime = _envelope.NextAttemptAt;
        var expectedDelay = TimeSpan.FromSeconds(300);
        var actualDelay = secondRetryTime!.Value - now;
        Assert.That(actualDelay, Is.GreaterThanOrEqualTo(expectedDelay - TimeSpan.FromMilliseconds(100)));
        Assert.That(actualDelay, Is.LessThanOrEqualTo(expectedDelay + TimeSpan.FromMilliseconds(100)));

        // Verify execution logs accumulated (from simulated orchestrator)
        Assert.That(_envelope.ExecutionLogs.Count(log => log.ActionType == ActionExecutionLogType.HandlerExecution), Is.EqualTo(2));
    }
    [Test]
    public void RecordAttemptFailure_PreservesFullErrorHistory()
    {
        // Arrange - simulate multiple failures with different error details
        var error1 = "Connection timeout";
        var error1Details = "System.Net.Http.HttpRequestException: Connection refused";

        var error2 = "Service unavailable";
        var error2Details = "System.Net.Http.HttpStatusCode: 503";

        // Simulate orchestrator adding HandlerExecution logs for both attempts
        AddMockHandlerExecutionLog(_envelope, 0, ActionExecutionStatus.Failed, error1, error1Details);
        AddMockHandlerExecutionLog(_envelope, 1, ActionExecutionStatus.Failed, error2, error2Details);

        // Act
        _envelope.RecordAttemptFailure(error1, error1Details);
        _envelope.RecordAttemptFailure(error2, error2Details);

        // Assert - full history preserved in HandlerExecution logs
        var logs = _envelope.ExecutionLogs.Where(log => log.ActionType == ActionExecutionLogType.HandlerExecution).ToList();
        Assert.That(logs.Count, Is.EqualTo(2));

        Assert.That(logs[0].ErrorMessage, Is.EqualTo(error1));
        Assert.That(logs[0].ErrorDetails, Is.EqualTo(error1Details));
        Assert.That(logs[0].AttemptNumber, Is.EqualTo(0));

        Assert.That(logs[1].ErrorMessage, Is.EqualTo(error2));
        Assert.That(logs[1].ErrorDetails, Is.EqualTo(error2Details));
        Assert.That(logs[1].AttemptNumber, Is.EqualTo(1));

    }
    [Test]
    public void MarkCompleted_AfterTransientFailures_PreservesFailureHistory()
    {
        // Arrange - fail twice then succeed
        // Simulate orchestrator adding HandlerExecution logs
        AddMockHandlerExecutionLog(_envelope, 0, ActionExecutionStatus.Failed, "Attempt 1 failed");
        _envelope.RecordAttemptFailure("Attempt 1 failed");
        
        AddMockHandlerExecutionLog(_envelope, 1, ActionExecutionStatus.Failed, "Attempt 2 failed");
        _envelope.RecordAttemptFailure("Attempt 2 failed");
        
        AddMockHandlerExecutionLog(_envelope, 2, ActionExecutionStatus.Completed);

        // Act
        _envelope.MarkCompleted();
    }

    #endregion

    #region Max Attempts -> Dead Letter

    [Test]
    public void RecordAttemptFailure_ThirdAttempt_SchedulesRetryWithThirdDelay()
    {
        // Arrange - simulate 3 failed attempts
        AddMockHandlerExecutionLog(_envelope, 0, ActionExecutionStatus.Failed, "Attempt 1 failed");
        _envelope.RecordAttemptFailure("Attempt 1 failed");
        
        AddMockHandlerExecutionLog(_envelope, 1, ActionExecutionStatus.Failed, "Attempt 2 failed");
        _envelope.RecordAttemptFailure("Attempt 2 failed");

        AddMockHandlerExecutionLog(_envelope, 2, ActionExecutionStatus.Failed, "Attempt 3 failed");

        // Act
        _envelope.RecordAttemptFailure("Attempt 3 failed");

        // Assert - third delay (900s) is scheduled
        Assert.That(_envelope.NextAttemptAt, Is.Not.Null);
        Assert.That(_envelope.Status, Is.EqualTo(ActionExecutionStatus.Failed));

        // Verify 900-second delay is used
        var expectedNextAttempt = DateTimeOffset.UtcNow.AddSeconds(900);
        Assert.That(_envelope.NextAttemptAt!.Value, Is.EqualTo(expectedNextAttempt).Within(TimeSpan.FromSeconds(2)));

        // Verify 3 logs recorded
        var handlerLogs = _envelope.ExecutionLogs.Where(log => log.ActionType == ActionExecutionLogType.HandlerExecution).ToList();
        Assert.That(handlerLogs.Count, Is.EqualTo(3));
    }

    [Test]
    public void RecordAttemptFailure_FourthAttempt_DoesNotScheduleRetry()
    {
        // Arrange - simulate 4 failed attempts
        AddMockHandlerExecutionLog(_envelope, 0, ActionExecutionStatus.Failed, "Attempt 1 failed");
        _envelope.RecordAttemptFailure("Attempt 1 failed");
        
        AddMockHandlerExecutionLog(_envelope, 1, ActionExecutionStatus.Failed, "Attempt 2 failed");
        _envelope.RecordAttemptFailure("Attempt 2 failed");
        
        AddMockHandlerExecutionLog(_envelope, 2, ActionExecutionStatus.Failed, "Attempt 3 failed");
        _envelope.RecordAttemptFailure("Attempt 3 failed");
        
        AddMockHandlerExecutionLog(_envelope, 3, ActionExecutionStatus.Failed, "Attempt 4 failed");

        // Act
        _envelope.RecordAttemptFailure("Attempt 4 failed");

        // Assert - no next attempt scheduled (all delays exhausted)
        Assert.That(_envelope.NextAttemptAt, Is.Null);
        Assert.That(_envelope.Status, Is.EqualTo(ActionExecutionStatus.Failed));

        // Verify 4 logs recorded
        var handlerLogs = _envelope.ExecutionLogs.Where(log => log.ActionType == ActionExecutionLogType.HandlerExecution).ToList();
        Assert.That(handlerLogs.Count, Is.EqualTo(4));
    }

    [Test]
    public void MarkDeadLettered_TransitionsToTerminalState()
    {
        // Arrange - simulate 4 failed attempts
        // Simulate orchestrator adding HandlerExecution logs for failed attempts
        AddMockHandlerExecutionLog(_envelope, 0, ActionExecutionStatus.Failed, "Attempt 1 failed");
        AddMockHandlerExecutionLog(_envelope, 1, ActionExecutionStatus.Failed, "Attempt 2 failed");
        AddMockHandlerExecutionLog(_envelope, 2, ActionExecutionStatus.Failed, "Attempt 3 failed");
        AddMockHandlerExecutionLog(_envelope, 3, ActionExecutionStatus.Failed, "Attempt 4 failed");

        // Act
        _envelope.MarkDeadLettered();

        // Assert
        Assert.That(_envelope.Status, Is.EqualTo(ActionExecutionStatus.DeadLettered));
        Assert.That(_envelope.CompletedAt, Is.Not.Null);
        Assert.That(_envelope.NextAttemptAt, Is.Null);

        // Verify dead-letter log entry created
        var deadLetterLog = _envelope.ExecutionLogs.FirstOrDefault(log => log.ActionType == ActionExecutionLogType.DeadLetter);
        Assert.That(deadLetterLog, Is.Not.Null);
        Assert.That(deadLetterLog!.Status, Is.EqualTo(ActionExecutionStatus.DeadLettered));
        Assert.That(deadLetterLog.ErrorMessage, Is.EqualTo("Dead-lettered after maximum retry attempts exceeded"));
    }

    [Test]
    public void MarkDeadLettered_IncludesFullErrorContext()
    {
        // Arrange - accumulated errors from failed attempts

        // Simulate orchestrator adding HandlerExecution logs with error details
        AddMockHandlerExecutionLog(_envelope, 0, ActionExecutionStatus.Failed, "Network error", "System.Net.Http.HttpRequestException");
        AddMockHandlerExecutionLog(_envelope, 1, ActionExecutionStatus.Failed, "Timeout error", "System.TimeoutException");
        AddMockHandlerExecutionLog(_envelope, 2, ActionExecutionStatus.Failed, "Service error", "System.ServiceException");
        AddMockHandlerExecutionLog(_envelope, 3, ActionExecutionStatus.Failed, "Final error", "System.FinalException");
        // Act
        _envelope.MarkDeadLettered();

        // Assert - all error details preserved in HandlerExecution logs
        var logs = _envelope.ExecutionLogs.ToList();
        var errorLogs = logs.Where(log => log.ActionType == ActionExecutionLogType.HandlerExecution && log.Status == ActionExecutionStatus.Failed).ToList();
        
        Assert.That(errorLogs.Count, Is.EqualTo(4));
        Assert.That(errorLogs[0].ErrorMessage, Is.EqualTo("Network error"));
        Assert.That(errorLogs[0].ErrorDetails, Is.EqualTo("System.Net.Http.HttpRequestException"));
        
        Assert.That(errorLogs[1].ErrorMessage, Is.EqualTo("Timeout error"));
        Assert.That(errorLogs[2].ErrorMessage, Is.EqualTo("Service error"));

        Assert.That(errorLogs[3].ErrorMessage, Is.EqualTo("Final error"));
        Assert.That(errorLogs[3].ErrorDetails, Is.EqualTo("System.FinalException"));

        // Dead-letter marker also present
        var deadLetterLog = logs.FirstOrDefault(log => log.ActionType == ActionExecutionLogType.DeadLetter);
        Assert.That(deadLetterLog, Is.Not.Null);

    }
    [Test]
    public void MarkDeadLettered_Idempotent_NoDoubleEntry()
    {
        // Arrange
        _envelope.MarkDeadLettered();
        var firstCallLogCount = _envelope.ExecutionLogs.Count;

        // Act - call again
        _envelope.MarkDeadLettered();

        // Assert - no additional log entry created
        Assert.That(_envelope.ExecutionLogs.Count, Is.EqualTo(firstCallLogCount));
        Assert.That(_envelope.Status, Is.EqualTo(ActionExecutionStatus.DeadLettered));
    }

    #endregion

    #region ShouldRetry Predicate

    [Test]
    public void ShouldRetry_PendingStatus_ReturnsFalse()
    {
        // Arrange - envelope is still pending
        Assert.That(_envelope.Status, Is.EqualTo(ActionExecutionStatus.Pending));

        // Act
        var result = _envelope.ShouldRetry();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void ShouldRetry_FailedStatusWithAttemptsRemaining_ReturnsTrue()
    {
        // Arrange - one failed attempt
        _envelope.RecordAttemptFailure("Attempt 1 failed");
        // Simulate orchestrator adding HandlerExecution log
        AddMockHandlerExecutionLog(_envelope, 0, ActionExecutionStatus.Failed);

        Assert.That(_envelope.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        // Act
        var result = _envelope.ShouldRetry();

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void ShouldRetry_MaxAttemptsReached_ReturnsFalse()
    {
        // Arrange - 4 failures = max attempts reached (4 HandlerExecution logs)
        // Simulate orchestrator adding HandlerExecution logs for 4 attempts (exceeds limit)
        AddMockHandlerExecutionLog(_envelope, 0, ActionExecutionStatus.Failed);
        AddMockHandlerExecutionLog(_envelope, 1, ActionExecutionStatus.Failed);
        AddMockHandlerExecutionLog(_envelope, 2, ActionExecutionStatus.Failed);
        AddMockHandlerExecutionLog(_envelope, 3, ActionExecutionStatus.Failed);
        // Act
        var result = _envelope.ShouldRetry();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void ShouldRetry_DeadLetteredStatus_ReturnsFalse()
    {
        // Arrange
        _envelope.MarkDeadLettered();

        // Act
        var result = _envelope.ShouldRetry();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void ShouldRetry_CompletedStatus_ReturnsFalse()
    {
        // Arrange
        _envelope.MarkCompleted();

        // Act
        var result = _envelope.ShouldRetry();

        // Assert
        Assert.That(result, Is.False);
    }

    #endregion

    #region Error Cases & Guards

    [Test]
    public void RecordAttemptFailure_OnDeadLetteredEnvelope_ThrowsInvalidOperation()
    {
        // Arrange
        _envelope.MarkDeadLettered();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _envelope.RecordAttemptFailure("Should fail"));

        Assert.That(ex.Message, Contains.Substring("dead-lettered"));
    }

    [Test]
    public void MarkCompleted_OnDeadLetteredEnvelope_Idempotent()
    {
        // Arrange
        _envelope.MarkDeadLettered();

        // Act - should not throw
        _envelope.MarkCompleted();

        // Assert - stays dead-lettered
        Assert.That(_envelope.Status, Is.EqualTo(ActionExecutionStatus.DeadLettered));
    }

    [Test]
    public void MarkCompleted_OnAlreadyCompleted_Idempotent()
    {
        // Arrange
        _envelope.MarkCompleted();
        var firstCallLogCount = _envelope.ExecutionLogs.Count;

        // Act
        _envelope.MarkCompleted();

        // Assert
        Assert.That(_envelope.Status, Is.EqualTo(ActionExecutionStatus.Completed));
        Assert.That(_envelope.ExecutionLogs.Count, Is.EqualTo(firstCallLogCount)); // No duplicate log
    }

    #endregion

    #region Constants & Policy Validation

    [Test]
    public void MaxRetryAttempts_ConstantIsThree()
    {
        // MaxRetryAttempts = 3 means: 1 initial attempt + 3 retry attempts = 4 total executions
        // This allows all 3 delay values (60s, 300s, 900s) to be used before dead-lettering
        Assert.That(CommandEnvelope.MaxRetryAttempts, Is.EqualTo(3));
    }

    [Test]
    public void RetryDelaysSeconds_MatchesPolicy()
    {
        var delays = CommandEnvelope.RetryDelaysSeconds;
        Assert.That(delays.Length, Is.EqualTo(3)); // 3 delays: 60s, 300s, 900s
        Assert.That(delays[0], Is.EqualTo(60));   // 1 minute
        Assert.That(delays[1], Is.EqualTo(300));  // 5 minutes
        Assert.That(delays[2], Is.EqualTo(900));  // 15 minutes
    }

    [Test]
    public void ExecutionLogAttemptNumbers_AreSequential()
    {
        // Arrange - simulate orchestrator adding HandlerExecution logs
        // Simulate orchestrator adding HandlerExecution logs
        AddMockHandlerExecutionLog(_envelope, 0, ActionExecutionStatus.Failed);
        AddMockHandlerExecutionLog(_envelope, 1, ActionExecutionStatus.Failed);
        AddMockHandlerExecutionLog(_envelope, 2, ActionExecutionStatus.Completed);

        // Act
        var logs = _envelope.ExecutionLogs.Where(log => log.ActionType == ActionExecutionLogType.HandlerExecution).ToList();

        // Assert
        Assert.That(logs[0].AttemptNumber, Is.EqualTo(0));
        Assert.That(logs[1].AttemptNumber, Is.EqualTo(1));
        Assert.That(logs[2].AttemptNumber, Is.EqualTo(2));
    }

    #endregion

    // Helper method to simulate orchestrator adding HandlerExecution logs
    private void AddMockHandlerExecutionLog(CommandEnvelope envelope, int attemptNumber, ActionExecutionStatus status, string? errorMessage = null, string? errorDetails = null)
    {
        if (envelope.ExecutionLogs.All(log => log.ActionType != ActionExecutionLogType.Execute || log.AttemptNumber != attemptNumber))
        {
            envelope.ExecutionLogs.Add(new ActionExecutionLog
            {
                CommandEnvelopeId = envelope.Id,
                ActionType = ActionExecutionLogType.Execute,
                Status = status,
                AttemptNumber = attemptNumber,
                ErrorMessage = errorMessage,
                ErrorDetails = errorDetails
            });
        }

        envelope.ExecutionLogs.Add(new ActionExecutionLog
        {
            CommandEnvelopeId = envelope.Id,
            ActionType = ActionExecutionLogType.HandlerExecution,
            Status = status,
            AttemptNumber = attemptNumber,
            HandlerTypeName = "TestHandler",
            ErrorMessage = errorMessage,
            ErrorDetails = errorDetails
        });
    }
}
