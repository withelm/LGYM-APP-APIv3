using FluentAssertions;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

/// <summary>
/// T12 Reliability Tests - Crash Window Recovery
/// Tests committed-intent recovery for intents that were persisted but not yet dispatched to scheduler.
/// </summary>
[TestFixture]
public sealed class ReliabilityCrashWindowTests : IntegrationTestBase
{
    [Test]
    public async Task CommittedCommandEnvelope_NotYetDispatched_RecoveredByDispatcher()
    {
        // Arrange - Manually insert a CommandEnvelope that was committed but not yet dispatched
        // (simulates crash after DB commit but before scheduler enqueue)
        var correlationId = Id<CorrelationScope>.New();
        var envelope = new CommandEnvelope
        {
            Id = Id<CommandEnvelope>.New(),
            CorrelationId = correlationId,
            CommandTypeFullName = "LgymApi.BackgroundWorker.Actions.UserRegisteredCommand",
            PayloadJson = "{\"UserId\":\"00000000-0000-0000-0000-000000000001\",\"Email\":\"crash-test@example.com\"}",
            Status = ActionExecutionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            DispatchedAt = null, // NOT YET DISPATCHED (crash window)
            SchedulerJobId = null
        };

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.CommandEnvelopes.Add(envelope);
            await db.SaveChangesAsync();
        }

        // Act - Invoke committed-intent dispatcher (recovery job)
        using (var scope = Factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<ICommittedIntentDispatcher>();
            await dispatcher.DispatchCommittedIntentsAsync(CancellationToken.None);
        }

        // Assert - Envelope is now dispatched
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var recovered = await db.CommandEnvelopes.FindAsync(envelope.Id);

            recovered.Should().NotBeNull();
            recovered!.DispatchedAt.Should().NotBeNull("recovery job should set DispatchedAt");
            recovered.SchedulerJobId.Should().NotBeNullOrEmpty("recovery job should assign scheduler job ID");
            recovered.Status.Should().Be(ActionExecutionStatus.Pending, 
                "envelope should remain Pending until background worker processes it");
        }
    }

    [Test]
    public async Task CommittedNotificationMessage_NotYetDispatched_RecoveredByDispatcher()
    {
        // Arrange - Manually insert a NotificationMessage that was committed but not yet dispatched
        var correlationId = Id<CorrelationScope>.New();
        var notification = new NotificationMessage
        {
            Id = Id<NotificationMessage>.New(),
            CorrelationId = correlationId,
            Channel = NotificationChannel.Email,
            Type = EmailNotificationTypes.Welcome,
            Recipient = "crash-notification@example.com",
            PayloadJson = "{\"RecipientEmail\":\"crash-notification@example.com\",\"UserName\":\"CrashUser\",\"CultureName\":\"en\"}",
            Status = EmailNotificationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            DispatchedAt = null, // NOT YET DISPATCHED (crash window)
            SchedulerJobId = null,
            Attempts = 0
        };

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.NotificationMessages.Add(notification);
            await db.SaveChangesAsync();
        }

        // Act - Invoke committed-intent dispatcher (recovery job)
        using (var scope = Factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<ICommittedIntentDispatcher>();
            await dispatcher.DispatchCommittedIntentsAsync(CancellationToken.None);
        }

        // Assert - Notification is now dispatched
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var recovered = await db.NotificationMessages.FindAsync(notification.Id);

            recovered.Should().NotBeNull();
            recovered!.DispatchedAt.Should().NotBeNull("recovery job should set DispatchedAt");
            recovered.SchedulerJobId.Should().NotBeNullOrEmpty("recovery job should assign scheduler job ID");
            recovered.Status.Should().Be(EmailNotificationStatus.Pending,
                "notification should remain Pending until background worker processes it");
        }
    }

    [Test]
    public async Task AlreadyDispatchedEnvelope_NotRedispatched()
    {
        // Arrange - Create an envelope that was already dispatched
        var correlationId = Id<CorrelationScope>.New();
        var envelope = new CommandEnvelope
        {
            Id = Id<CommandEnvelope>.New(),
            CorrelationId = correlationId,
            CommandTypeFullName = "LgymApi.BackgroundWorker.Actions.UserRegisteredCommand",
            PayloadJson = "{\"UserId\":\"00000000-0000-0000-0000-000000000002\",\"Email\":\"already-dispatched@example.com\"}",
            Status = ActionExecutionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            DispatchedAt = DateTimeOffset.UtcNow.AddMinutes(-4), // ALREADY DISPATCHED
            SchedulerJobId = "existing-job-id-123"
        };

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.CommandEnvelopes.Add(envelope);
            await db.SaveChangesAsync();
        }

        var originalDispatchedAt = envelope.DispatchedAt;
        var originalJobId = envelope.SchedulerJobId;

        // Act - Invoke recovery dispatcher
        using (var scope = Factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<ICommittedIntentDispatcher>();
            await dispatcher.DispatchCommittedIntentsAsync(CancellationToken.None);
        }

        // Assert - Envelope is NOT re-dispatched (idempotent recovery)
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var unchanged = await db.CommandEnvelopes.FindAsync(envelope.Id);

            unchanged.Should().NotBeNull();
            unchanged!.DispatchedAt.Should().Be(originalDispatchedAt, 
                "recovery should skip already-dispatched envelopes");
            unchanged.SchedulerJobId.Should().Be(originalJobId,
                "recovery should not overwrite existing scheduler job ID");
        }
    }

    [Test]
    public async Task ProcessingEnvelope_NotRedispatched()
    {
        // Arrange - Create an envelope that is currently being processed
        var correlationId = Id<CorrelationScope>.New();
        var envelope = new CommandEnvelope
        {
            Id = Id<CommandEnvelope>.New(),
            CorrelationId = correlationId,
            CommandTypeFullName = "LgymApi.BackgroundWorker.Actions.UserRegisteredCommand",
            PayloadJson = "{\"UserId\":\"00000000-0000-0000-0000-000000000003\",\"Email\":\"processing@example.com\"}",
            Status = ActionExecutionStatus.Processing,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            DispatchedAt = DateTimeOffset.UtcNow.AddMinutes(-4),
            SchedulerJobId = "processing-job-id-456"
        };

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.CommandEnvelopes.Add(envelope);
            await db.SaveChangesAsync();
        }

        // Act - Invoke recovery dispatcher
        using (var scope = Factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<ICommittedIntentDispatcher>();
            await dispatcher.DispatchCommittedIntentsAsync(CancellationToken.None);
        }

        // Assert - Processing envelope is not re-dispatched (at-least-once protection)
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var unchanged = await db.CommandEnvelopes.FindAsync(envelope.Id);

            unchanged.Should().NotBeNull();
            unchanged!.Status.Should().Be(ActionExecutionStatus.Processing,
                "recovery should skip envelopes currently being processed");
        }
    }

    [Test]
    public async Task CompletedEnvelope_NotRedispatched()
    {
        // Arrange - Create an envelope that was already completed
        var correlationId = Id<CorrelationScope>.New();
        var envelope = new CommandEnvelope
        {
            Id = Id<CommandEnvelope>.New(),
            CorrelationId = correlationId,
            CommandTypeFullName = "LgymApi.BackgroundWorker.Actions.UserRegisteredCommand",
            PayloadJson = "{\"UserId\":\"00000000-0000-0000-0000-000000000004\",\"Email\":\"completed@example.com\"}",
            Status = ActionExecutionStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            DispatchedAt = DateTimeOffset.UtcNow.AddMinutes(-9),
            CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-8),
            SchedulerJobId = "completed-job-id-789"
        };

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.CommandEnvelopes.Add(envelope);
            await db.SaveChangesAsync();
        }

        // Act - Invoke recovery dispatcher
        using (var scope = Factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<ICommittedIntentDispatcher>();
            await dispatcher.DispatchCommittedIntentsAsync(CancellationToken.None);
        }

        // Assert - Completed envelope is not redispatched
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var unchanged = await db.CommandEnvelopes.FindAsync(envelope.Id);

            unchanged.Should().NotBeNull();
            unchanged!.Status.Should().Be(ActionExecutionStatus.Completed,
                "recovery should skip already-completed envelopes");
            unchanged.CompletedAt.Should().NotBeNull();
        }
    }

    [Test]
    public async Task DeadLetteredEnvelope_NotRedispatched()
    {
        // Arrange - Create a dead-lettered envelope
        var correlationId = Id<CorrelationScope>.New();
        var envelope = new CommandEnvelope
        {
            Id = Id<CommandEnvelope>.New(),
            CorrelationId = correlationId,
            CommandTypeFullName = "LgymApi.BackgroundWorker.Actions.UnknownCommand",
            PayloadJson = "{\"Invalid\":\"Poison Message\"}",
            Status = ActionExecutionStatus.DeadLettered,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            DispatchedAt = DateTimeOffset.UtcNow.AddMinutes(-9),
            SchedulerJobId = "dead-letter-job-id"
        };

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.CommandEnvelopes.Add(envelope);
            await db.SaveChangesAsync();
        }

        // Act - Invoke recovery dispatcher
        using (var scope = Factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<ICommittedIntentDispatcher>();
            await dispatcher.DispatchCommittedIntentsAsync(CancellationToken.None);
        }

        // Assert - Dead-lettered envelope is not redispatched
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var unchanged = await db.CommandEnvelopes.FindAsync(envelope.Id);

            unchanged.Should().NotBeNull();
            unchanged!.Status.Should().Be(ActionExecutionStatus.DeadLettered,
                "recovery should skip dead-lettered envelopes");
        }
    }

    [Test]
    public async Task InspectAsync_PendingNotification_WithMissingSchedulerJob_IsRecoverable()
    {
        // Arrange - simulate a dispatched notification whose Hangfire job vanished after the durable row was written
        const string missingJobId = "missing-email-job";
        var notification = new NotificationMessage
        {
            Id = Id<NotificationMessage>.New(),
            CorrelationId = Id<CorrelationScope>.New(),
            Channel = NotificationChannel.Email,
            Type = EmailNotificationTypes.Welcome,
            Recipient = "missing-job@example.com",
            PayloadJson = "{}",
            Status = EmailNotificationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            DispatchedAt = DateTimeOffset.UtcNow.AddMinutes(-4),
            SchedulerJobId = missingJobId,
            Attempts = 0
        };

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.NotificationMessages.Add(notification);
            await db.SaveChangesAsync();
        }

        var hangfireStateReader = Factory.Services.GetRequiredService<InMemoryHangfireJobStateReader>();
        hangfireStateReader.SetMissing(missingJobId);

        using var inspectionScope = Factory.Services.CreateScope();
        var inspector = inspectionScope.ServiceProvider.GetRequiredService<IEmailNotificationRecoverabilityInspector>();

        // Act
        var result = await inspector.InspectAsync();

        // Assert
        result.BrokenJobsFound.Should().Be(1);
        result.RecoverableNotifications.Should().Be(1);
        result.ActiveJobsSkipped.Should().Be(0);
        result.AlreadySentSkipped.Should().Be(0);
        result.DeadLetterSkipped.Should().Be(0);

        result.Notifications.Should().ContainSingle(item =>
            item.NotificationId == notification.Id
            && item.Disposition == EmailNotificationRecoverabilityDisposition.Recoverable
            && item.HangfireState == null
            && item.HasBrokenSchedulerState);
    }

    [Test]
    public async Task InspectAsync_SentNotification_WithMissingSchedulerJob_IsSkipped()
    {
        // Arrange - preserve duplicate-send protection even when Hangfire metadata is stale
        const string missingJobId = "sent-missing-email-job";
        var notification = new NotificationMessage
        {
            Id = Id<NotificationMessage>.New(),
            CorrelationId = Id<CorrelationScope>.New(),
            Channel = NotificationChannel.Email,
            Type = EmailNotificationTypes.Welcome,
            Recipient = "already-sent@example.com",
            PayloadJson = "{}",
            Status = EmailNotificationStatus.Sent,
            SentAt = DateTimeOffset.UtcNow.AddMinutes(-3),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            DispatchedAt = DateTimeOffset.UtcNow.AddMinutes(-4),
            SchedulerJobId = missingJobId,
            Attempts = 1
        };

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.NotificationMessages.Add(notification);
            await db.SaveChangesAsync();
        }

        var hangfireStateReader = Factory.Services.GetRequiredService<InMemoryHangfireJobStateReader>();
        hangfireStateReader.SetMissing(missingJobId);

        using var inspectionScope = Factory.Services.CreateScope();
        var inspector = inspectionScope.ServiceProvider.GetRequiredService<IEmailNotificationRecoverabilityInspector>();

        // Act
        var result = await inspector.InspectAsync();

        // Assert
        result.BrokenJobsFound.Should().Be(0);
        result.RecoverableNotifications.Should().Be(0);
        result.ActiveJobsSkipped.Should().Be(0);
        result.AlreadySentSkipped.Should().Be(1);
        result.DeadLetterSkipped.Should().Be(0);

        result.Notifications.Should().ContainSingle(item =>
            item.NotificationId == notification.Id
            && item.Disposition == EmailNotificationRecoverabilityDisposition.AlreadySentSkipped
            && item.HangfireState == null
            && !item.HasBrokenSchedulerState);
    }

    [Test]
    public async Task RemediateAsync_PendingNotification_ReplaysAndReconcilesStaleJob()
    {
        const string staleJobId = "stale-email-job";

        var notification = new NotificationMessage
        {
            Id = Id<NotificationMessage>.New(),
            CorrelationId = Id<CorrelationScope>.New(),
            Channel = NotificationChannel.Email,
            Type = EmailNotificationTypes.Welcome,
            Recipient = "replay@example.com",
            PayloadJson = "{}",
            Status = EmailNotificationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            DispatchedAt = DateTimeOffset.UtcNow.AddMinutes(-4),
            SchedulerJobId = staleJobId,
            Attempts = 0
        };

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.NotificationMessages.Add(notification);
            await db.SaveChangesAsync();
        }

        var hangfireStateReader = Factory.Services.GetRequiredService<InMemoryHangfireJobStateReader>();
        hangfireStateReader.SetBroken(staleJobId, "Failed");

        using (var scope = Factory.Services.CreateScope())
        {
            var remediator = scope.ServiceProvider.GetRequiredService<IEmailNotificationRecoverabilityRemediator>();
            await remediator.RemediateAsync(CancellationToken.None);
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var recovered = await db.NotificationMessages.FindAsync(notification.Id);

            recovered.Should().NotBeNull();
            recovered!.Status.Should().Be(EmailNotificationStatus.Pending);
            recovered.DispatchedAt.Should().NotBeNull();
            recovered.SchedulerJobId.Should().NotBeNullOrWhiteSpace();
            recovered.SchedulerJobId.Should().NotBe(staleJobId);
        }

        var reconciler = Factory.Services.GetRequiredService<InMemoryHangfireJobReconciler>();
        reconciler.DeletedJobIds.Should().ContainSingle(x => x == staleJobId);
    }

    [Test]
    public async Task RemediateAsync_SentNotification_RemainsUnchanged()
    {
        const string staleJobId = "sent-stale-email-job";

        var notification = new NotificationMessage
        {
            Id = Id<NotificationMessage>.New(),
            CorrelationId = Id<CorrelationScope>.New(),
            Channel = NotificationChannel.Email,
            Type = EmailNotificationTypes.Welcome,
            Recipient = "sent@example.com",
            PayloadJson = "{}",
            Status = EmailNotificationStatus.Sent,
            SentAt = DateTimeOffset.UtcNow.AddMinutes(-3),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            DispatchedAt = DateTimeOffset.UtcNow.AddMinutes(-4),
            SchedulerJobId = staleJobId,
            Attempts = 1
        };

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.NotificationMessages.Add(notification);
            await db.SaveChangesAsync();
        }

        var hangfireStateReader = Factory.Services.GetRequiredService<InMemoryHangfireJobStateReader>();
        hangfireStateReader.SetBroken(staleJobId, "Deleted");

        using (var scope = Factory.Services.CreateScope())
        {
            var remediator = scope.ServiceProvider.GetRequiredService<IEmailNotificationRecoverabilityRemediator>();
            await remediator.RemediateAsync(CancellationToken.None);
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var recovered = await db.NotificationMessages.FindAsync(notification.Id);

            recovered.Should().NotBeNull();
            recovered!.Status.Should().Be(EmailNotificationStatus.Sent);
            recovered.SentAt.Should().NotBeNull();
            recovered.SchedulerJobId.Should().Be(staleJobId);
        }

        var reconciler = Factory.Services.GetRequiredService<InMemoryHangfireJobReconciler>();
        reconciler.DeletedJobIds.Should().BeEmpty();
    }
}
