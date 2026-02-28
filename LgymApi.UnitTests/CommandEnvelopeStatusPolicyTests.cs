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
            Id = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
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

        // Act
        _envelope.RecordAttemptFailure(errorMsg, errorDetails);

        // Assert
        Assert.That(_envelope.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(_envelope.LastAttemptAt, Is.Not.Null);
        Assert.That(_envelope.NextAttemptAt, Is.Not.Null);

        // Verify execution log entry created
        var logEntry = _envelope.ExecutionLogs.Single();
        Assert.That(logEntry.ActionType, Is.EqualTo("Execute"));
        Assert.That(logEntry.Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(logEntry.AttemptNumber, Is.EqualTo(0));
        Assert.That(logEntry.ErrorMessage, Is.EqualTo(errorMsg));
        Assert.That(logEntry.ErrorDetails, Is.EqualTo(errorDetails));
    }

    [Test]
    public void RecordAttemptFailure_FirstAttempt_SchedulesCorrectBackoff()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

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
        // Arrange
        _envelope.RecordAttemptFailure("Attempt 1 failed");
        var firstRetryTime = _envelope.NextAttemptAt;

        // Simulate retry execution and second failure
        _envelope.Status = ActionExecutionStatus.Failed; // Would be retried by orchestrator
        var now = DateTimeOffset.UtcNow;

        // Act
        _envelope.RecordAttemptFailure("Attempt 2 failed");

        // Assert - should schedule 300 second (5m) backoff for second retry
        var secondRetryTime = _envelope.NextAttemptAt;
        var expectedDelay = TimeSpan.FromSeconds(300);
        var actualDelay = secondRetryTime!.Value - now;
        Assert.That(actualDelay, Is.GreaterThanOrEqualTo(expectedDelay - TimeSpan.FromMilliseconds(100)));
        Assert.That(actualDelay, Is.LessThanOrEqualTo(expectedDelay + TimeSpan.FromMilliseconds(100)));

        // Verify execution logs accumulated
        Assert.That(_envelope.ExecutionLogs.Count, Is.EqualTo(2));
    }

    [Test]
    public void RecordAttemptFailure_PreservesFullErrorHistory()
    {
        // Arrange - simulate multiple failures with different error details
        var error1 = "Connection timeout";
        var error1Details = "System.Net.Http.HttpRequestException: Connection refused";

        var error2 = "Service unavailable";
        var error2Details = "System.Net.Http.HttpStatusCode: 503";

        // Act
        _envelope.RecordAttemptFailure(error1, error1Details);
        _envelope.RecordAttemptFailure(error2, error2Details);

        // Assert - full history preserved
        var logs = _envelope.ExecutionLogs.Where(log => log.ActionType == "Execute").ToList();
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
        _envelope.RecordAttemptFailure("Attempt 1 failed");
        _envelope.RecordAttemptFailure("Attempt 2 failed");

        // Act
        _envelope.MarkCompleted();

        // Assert
        Assert.That(_envelope.Status, Is.EqualTo(ActionExecutionStatus.Completed));
        Assert.That(_envelope.CompletedAt, Is.Not.Null);
        Assert.That(_envelope.NextAttemptAt, Is.Null);

        // Verify history preserved
        var logs = _envelope.ExecutionLogs.ToList();
        Assert.That(logs.Count, Is.EqualTo(3)); // 2 failures + 1 success
        
        // Last entry should be success
        var successLog = logs.Last();
        Assert.That(successLog.ActionType, Is.EqualTo("Execute"));
        Assert.That(successLog.Status, Is.EqualTo(ActionExecutionStatus.Completed));
        Assert.That(successLog.ErrorMessage, Is.Null);
        Assert.That(successLog.ErrorDetails, Is.Null);

        // Earlier entries should still contain failure details
        var failureLogs = logs.Where(log => log.Status == ActionExecutionStatus.Failed).ToList();
        Assert.That(failureLogs.Count, Is.EqualTo(2));
        Assert.That(failureLogs[0].ErrorMessage, Is.EqualTo("Attempt 1 failed"));
        Assert.That(failureLogs[1].ErrorMessage, Is.EqualTo("Attempt 2 failed"));
    }

    #endregion

    #region Max Attempts -> Dead Letter

    [Test]
    public void RecordAttemptFailure_ThirdAttempt_SchedulesRetryWithThirdDelay()
    {
        // Arrange
        _envelope.RecordAttemptFailure("Attempt 1 failed");
        _envelope.RecordAttemptFailure("Attempt 2 failed");

        // Act
        _envelope.RecordAttemptFailure("Attempt 3 failed");

        // Assert - third delay (900s) is scheduled
        Assert.That(_envelope.NextAttemptAt, Is.Not.Null);
        Assert.That(_envelope.Status, Is.EqualTo(ActionExecutionStatus.Failed));

        // Verify 900-second delay is used
        var expectedNextAttempt = DateTimeOffset.UtcNow.AddSeconds(900);
        Assert.That(_envelope.NextAttemptAt!.Value, Is.EqualTo(expectedNextAttempt).Within(TimeSpan.FromSeconds(2)));

        // Verify 3 logs recorded
        var executeLogs = _envelope.ExecutionLogs.Where(log => log.ActionType == "Execute").ToList();
        Assert.That(executeLogs.Count, Is.EqualTo(3));
    }

    [Test]
    public void RecordAttemptFailure_FourthAttempt_DoesNotScheduleRetry()
    {
        // Arrange
        _envelope.RecordAttemptFailure("Attempt 1 failed");
        _envelope.RecordAttemptFailure("Attempt 2 failed");
        _envelope.RecordAttemptFailure("Attempt 3 failed");

        // Act
        _envelope.RecordAttemptFailure("Attempt 4 failed");

        // Assert - no next attempt scheduled (all delays exhausted)
        Assert.That(_envelope.NextAttemptAt, Is.Null);
        Assert.That(_envelope.Status, Is.EqualTo(ActionExecutionStatus.Failed));

        // Verify 4 logs recorded
        var executeLogs = _envelope.ExecutionLogs.Where(log => log.ActionType == "Execute").ToList();
        Assert.That(executeLogs.Count, Is.EqualTo(4));
    }

    [Test]
    public void MarkDeadLettered_TransitionsToTerminalState()
    {
        // Arrange - simulate 4 failed attempts
        _envelope.RecordAttemptFailure("Attempt 1 failed");
        _envelope.RecordAttemptFailure("Attempt 2 failed");
        _envelope.RecordAttemptFailure("Attempt 3 failed");
        _envelope.RecordAttemptFailure("Attempt 4 failed");

        // Act
        _envelope.MarkDeadLettered();

        // Assert
        Assert.That(_envelope.Status, Is.EqualTo(ActionExecutionStatus.DeadLettered));
        Assert.That(_envelope.CompletedAt, Is.Not.Null);
        Assert.That(_envelope.NextAttemptAt, Is.Null);

        // Verify dead-letter log entry created
        var deadLetterLog = _envelope.ExecutionLogs.FirstOrDefault(log => log.ActionType == "DeadLetter");
        Assert.That(deadLetterLog, Is.Not.Null);
        Assert.That(deadLetterLog!.Status, Is.EqualTo(ActionExecutionStatus.DeadLettered));
        Assert.That(deadLetterLog.ErrorMessage, Is.EqualTo("Dead-lettered after maximum retry attempts exceeded"));
    }

    [Test]
    public void MarkDeadLettered_IncludesFullErrorContext()
    {
        // Arrange - accumulated errors from failed attempts
        _envelope.RecordAttemptFailure("Network error", "System.Net.Http.HttpRequestException");
        _envelope.RecordAttemptFailure("Timeout error", "System.TimeoutException");
        _envelope.RecordAttemptFailure("Service error", "System.ServiceException");
        _envelope.RecordAttemptFailure("Final error", "System.FinalException");

        // Act
        _envelope.MarkDeadLettered();

        // Assert - all error details preserved
        var logs = _envelope.ExecutionLogs.ToList();
        var errorLogs = logs.Where(log => log.ActionType == "Execute" && log.Status == ActionExecutionStatus.Failed).ToList();
        
        Assert.That(errorLogs.Count, Is.EqualTo(4));
        Assert.That(errorLogs[0].ErrorMessage, Is.EqualTo("Network error"));
        Assert.That(errorLogs[0].ErrorDetails, Is.EqualTo("System.Net.Http.HttpRequestException"));
        
        Assert.That(errorLogs[1].ErrorMessage, Is.EqualTo("Timeout error"));
        Assert.That(errorLogs[2].ErrorMessage, Is.EqualTo("Service error"));

        Assert.That(errorLogs[2].ErrorMessage, Is.EqualTo("Service error"));
        
        Assert.That(errorLogs[3].ErrorMessage, Is.EqualTo("Final error"));
        Assert.That(errorLogs[3].ErrorDetails, Is.EqualTo("System.FinalException"));

        // Dead-letter marker also present
        var deadLetterLog = logs.FirstOrDefault(log => log.ActionType == "DeadLetter");
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
        // Arrange
        _envelope.RecordAttemptFailure("Failed attempt");
        Assert.That(_envelope.Status, Is.EqualTo(ActionExecutionStatus.Failed));

        // Act
        var result = _envelope.ShouldRetry();

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void ShouldRetry_MaxAttemptsReached_ReturnsFalse()
    {
        // Arrange - 4 failures = max attempts reached
        _envelope.RecordAttemptFailure("Attempt 1");
        _envelope.RecordAttemptFailure("Attempt 2");
        _envelope.RecordAttemptFailure("Attempt 3");
        _envelope.RecordAttemptFailure("Attempt 4");

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
        // Arrange
        _envelope.RecordAttemptFailure("Attempt 1");
        _envelope.RecordAttemptFailure("Attempt 2");
        _envelope.MarkCompleted();

        // Act
        var logs = _envelope.ExecutionLogs.Where(log => log.ActionType == "Execute").ToList();

        // Assert
        Assert.That(logs[0].AttemptNumber, Is.EqualTo(0));
        Assert.That(logs[1].AttemptNumber, Is.EqualTo(1));
        Assert.That(logs[2].AttemptNumber, Is.EqualTo(2));
    }

    #endregion
}
