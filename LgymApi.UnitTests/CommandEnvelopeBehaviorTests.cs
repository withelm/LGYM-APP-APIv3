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

        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.Pending));
        Assert.That(envelope.ProcessingStartedAtUtc, Is.Null);
        Assert.That(envelope.DispatchedAt, Is.Null);
        Assert.That(envelope.SchedulerJobId, Is.Null);
        Assert.That(envelope.NextAttemptAt, Is.EqualTo(DateTimeOffset.UtcNow).Within(TimeSpan.FromSeconds(2)));

        var executeLogs = envelope.ExecutionLogs.Where(log => log.ActionType == ActionExecutionLogType.Execute).ToList();
        Assert.That(executeLogs, Has.Exactly(2).Items);
        Assert.That(executeLogs[^1].Status, Is.EqualTo(ActionExecutionStatus.Failed));
        Assert.That(executeLogs[^1].ErrorMessage, Is.EqualTo("lease expired"));
        Assert.That(executeLogs[^1].AttemptNumber, Is.EqualTo(1));
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

        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.Pending));
        Assert.That(envelope.SchedulerJobId, Is.EqualTo("job-123"));
        Assert.That(envelope.ExecutionLogs, Is.Empty);
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

        Assert.That(envelope.GetExecutionAttemptCount(), Is.EqualTo(2));
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

        Assert.That(envelope.Status, Is.EqualTo(ActionExecutionStatus.DeadLettered));
        Assert.That(envelope.CompletedAt, Is.Not.Null);
        var deadLetter = envelope.ExecutionLogs.Single(log => log.ActionType == ActionExecutionLogType.DeadLetter);
        Assert.That(deadLetter.ErrorMessage, Is.EqualTo("manual dead-letter"));
        Assert.That(deadLetter.ErrorDetails, Is.EqualTo("payload poisoned"));
        Assert.That(deadLetter.AttemptNumber, Is.EqualTo(1));
    }
}
