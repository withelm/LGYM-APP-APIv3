using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class CommandEnvelopeBehaviorTests
{
    [Test]
    public void ResetStaleProcessing_WhenEnvelopeIsProcessing_RequeuesAndClearsLeaseMetadata()
    {
        var envelope = new CommandEnvelope
        {
            Id = Id<CommandEnvelope>.New(),
            CorrelationId = Id<CorrelationScope>.New(),
            PayloadJson = "{}",
            CommandTypeFullName = "Tests.SampleCommand",
            Status = ActionExecutionStatus.Processing,
            ProcessingStartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            DispatchedAt = DateTimeOffset.UtcNow.AddMinutes(-9),
            SchedulerJobId = "job-123"
        };
        envelope.ExecutionLogs.Add(new ActionExecutionLog
        {
            Id = Id<ActionExecutionLog>.New(),
            CommandEnvelopeId = envelope.Id,
            ActionType = ActionExecutionLogType.Execute,
            Status = ActionExecutionStatus.Processing,
            AttemptNumber = 0
        });

        envelope.ResetStaleProcessing("lease expired");

        envelope.Status.Should().Be(ActionExecutionStatus.Pending);
        envelope.ProcessingStartedAtUtc.Should().BeNull();
        envelope.DispatchedAt.Should().BeNull();
        envelope.SchedulerJobId.Should().BeNull();
        envelope.NextAttemptAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));

        var executeLogs = envelope.ExecutionLogs.Where(log => log.ActionType == ActionExecutionLogType.Execute).ToList();
        executeLogs.Should().HaveCount(2);
        executeLogs[^1].Status.Should().Be(ActionExecutionStatus.Failed);
        executeLogs[^1].ErrorMessage.Should().Be("lease expired");
        executeLogs[^1].AttemptNumber.Should().Be(1);
    }

    [Test]
    public void ResetStaleProcessing_WhenEnvelopeIsNotProcessing_DoesNothing()
    {
        var envelope = new CommandEnvelope
        {
            Id = Id<CommandEnvelope>.New(),
            CorrelationId = Id<CorrelationScope>.New(),
            PayloadJson = "{}",
            CommandTypeFullName = "Tests.SampleCommand",
            Status = ActionExecutionStatus.Pending,
            SchedulerJobId = "job-123"
        };

        envelope.ResetStaleProcessing("ignored");

        envelope.Status.Should().Be(ActionExecutionStatus.Pending);
        envelope.SchedulerJobId.Should().Be("job-123");
        envelope.ExecutionLogs.Should().BeEmpty();
    }

    [Test]
    public void GetExecutionAttemptCount_CountsOnlyExecuteLogs()
    {
        var envelope = new CommandEnvelope
        {
            Id = Id<CommandEnvelope>.New(),
            CorrelationId = Id<CorrelationScope>.New(),
            PayloadJson = "{}",
            CommandTypeFullName = "Tests.SampleCommand"
        };

        envelope.ExecutionLogs.Add(new ActionExecutionLog { Id = Id<ActionExecutionLog>.New(), CommandEnvelopeId = envelope.Id, ActionType = ActionExecutionLogType.Execute, AttemptNumber = 0 });
        envelope.ExecutionLogs.Add(new ActionExecutionLog { Id = Id<ActionExecutionLog>.New(), CommandEnvelopeId = envelope.Id, ActionType = ActionExecutionLogType.HandlerExecution, AttemptNumber = 0, HandlerTypeName = "Handler" });
        envelope.ExecutionLogs.Add(new ActionExecutionLog { Id = Id<ActionExecutionLog>.New(), CommandEnvelopeId = envelope.Id, ActionType = ActionExecutionLogType.Execute, AttemptNumber = 1 });
        envelope.ExecutionLogs.Add(new ActionExecutionLog { Id = Id<ActionExecutionLog>.New(), CommandEnvelopeId = envelope.Id, ActionType = ActionExecutionLogType.DeadLetter, AttemptNumber = 2 });

        envelope.GetExecutionAttemptCount().Should().Be(2);
    }

    [Test]
    public void MarkDeadLettered_WithCustomReason_AddsDeadLetterLogWithProvidedDetails()
    {
        var envelope = new CommandEnvelope
        {
            Id = Id<CommandEnvelope>.New(),
            CorrelationId = Id<CorrelationScope>.New(),
            PayloadJson = "{}",
            CommandTypeFullName = "Tests.SampleCommand"
        };
        envelope.ExecutionLogs.Add(new ActionExecutionLog
        {
            Id = Id<ActionExecutionLog>.New(),
            CommandEnvelopeId = envelope.Id,
            ActionType = ActionExecutionLogType.Execute,
            AttemptNumber = 0,
            Status = ActionExecutionStatus.Failed
        });

        envelope.MarkDeadLettered("manual dead-letter", "payload poisoned");

        envelope.Status.Should().Be(ActionExecutionStatus.DeadLettered);
        envelope.CompletedAt.Should().NotBeNull();
        var deadLetter = envelope.ExecutionLogs.Single(log => log.ActionType == ActionExecutionLogType.DeadLetter);
        deadLetter.ErrorMessage.Should().Be("manual dead-letter");
        deadLetter.ErrorDetails.Should().Be("payload poisoned");
        deadLetter.AttemptNumber.Should().Be(1);
    }
}
