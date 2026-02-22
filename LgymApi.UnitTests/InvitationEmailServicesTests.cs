using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class InvitationEmailServicesTests
{
    [Test]
    public async Task Scheduler_WhenNoExistingNotification_AddsAndEnqueues()
    {
        var repository = new FakeNotificationRepository();
        var scheduler = new FakeBackgroundScheduler();
        var unitOfWork = new FakeUnitOfWork();

        var service = new InvitationEmailSchedulerService(
            repository,
            scheduler,
            unitOfWork,
            new EnabledFeature(),
            NullLogger<InvitationEmailSchedulerService>.Instance);

        await service.ScheduleInvitationCreatedAsync(new InvitationEmailPayload
        {
            InvitationId = Guid.NewGuid(),
            InvitationCode = "ABC123",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            TrainerName = "Coach",
            RecipientEmail = "trainee@example.com",
            CultureName = "en-US"
        });

        Assert.Multiple(() =>
        {
            Assert.That(repository.Added, Has.Count.EqualTo(1));
            Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(1));
            Assert.That(scheduler.EnqueuedNotificationIds, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Scheduler_WhenExistingFailedAtLimit_DoesNotEnqueue()
    {
        var existing = new EmailNotificationLog
        {
            Id = Guid.NewGuid(),
            Status = EmailNotificationStatus.Failed,
            Attempts = 5,
            Type = InvitationEmailSchedulerService.NotificationType,
            CorrelationId = Guid.NewGuid(),
            RecipientEmail = "trainee@example.com",
            PayloadJson = "{}"
        };

        var repository = new FakeNotificationRepository { ExistingByCorrelation = existing };
        var scheduler = new FakeBackgroundScheduler();
        var unitOfWork = new FakeUnitOfWork();

        var service = new InvitationEmailSchedulerService(
            repository,
            scheduler,
            unitOfWork,
            new EnabledFeature(),
            NullLogger<InvitationEmailSchedulerService>.Instance);

        await service.ScheduleInvitationCreatedAsync(new InvitationEmailPayload
        {
            InvitationId = existing.CorrelationId,
            InvitationCode = "ABC123",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            TrainerName = "Coach",
            RecipientEmail = existing.RecipientEmail,
            CultureName = "en-US"
        });

        Assert.Multiple(() =>
        {
            Assert.That(scheduler.EnqueuedNotificationIds, Is.Empty);
            Assert.That(repository.Added, Is.Empty);
        });
    }

    [Test]
    public async Task Scheduler_WhenEmailNotificationsDisabled_DoesNotCreateOrEnqueue()
    {
        var repository = new FakeNotificationRepository();
        var scheduler = new FakeBackgroundScheduler();
        var unitOfWork = new FakeUnitOfWork();

        var service = new InvitationEmailSchedulerService(
            repository,
            scheduler,
            unitOfWork,
            new DisabledFeature(),
            NullLogger<InvitationEmailSchedulerService>.Instance);

        await service.ScheduleInvitationCreatedAsync(new InvitationEmailPayload
        {
            InvitationId = Guid.NewGuid(),
            InvitationCode = "ABC123",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            TrainerName = "Coach",
            RecipientEmail = "trainee@example.com",
            CultureName = "en-US"
        });

        Assert.Multiple(() =>
        {
            Assert.That(repository.Added, Is.Empty);
            Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(0));
            Assert.That(scheduler.EnqueuedNotificationIds, Is.Empty);
        });
    }

    [Test]
    public async Task Scheduler_WhenConcurrentInsertDetected_EnqueuesExistingNotification()
    {
        var existing = new EmailNotificationLog
        {
            Id = Guid.NewGuid(),
            Status = EmailNotificationStatus.Pending,
            Type = InvitationEmailSchedulerService.NotificationType,
            CorrelationId = Guid.NewGuid(),
            RecipientEmail = "trainee@example.com",
            PayloadJson = "{}"
        };

        var repository = new FakeNotificationRepository
        {
            ExistingByCorrelationOnSecondLookup = existing
        };
        var scheduler = new FakeBackgroundScheduler();
        var unitOfWork = new FakeUnitOfWork { ThrowOnSave = true };

        var service = new InvitationEmailSchedulerService(
            repository,
            scheduler,
            unitOfWork,
            new EnabledFeature(),
            NullLogger<InvitationEmailSchedulerService>.Instance);

        await service.ScheduleInvitationCreatedAsync(new InvitationEmailPayload
        {
            InvitationId = existing.CorrelationId,
            InvitationCode = "ABC123",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            TrainerName = "Coach",
            RecipientEmail = existing.RecipientEmail,
            CultureName = "en-US"
        });

        Assert.Multiple(() =>
        {
            Assert.That(scheduler.EnqueuedNotificationIds, Has.Count.EqualTo(1));
            Assert.That(scheduler.EnqueuedNotificationIds[0], Is.EqualTo(existing.Id));
        });
    }

    [Test]
    public async Task JobHandler_WhenTemplateComposerThrows_MarksFailedAndSaves()
    {
        var notification = new EmailNotificationLog
        {
            Id = Guid.NewGuid(),
            Status = EmailNotificationStatus.Pending,
            Attempts = 0,
            Type = InvitationEmailSchedulerService.NotificationType,
            CorrelationId = Guid.NewGuid(),
            RecipientEmail = "trainee@example.com",
            PayloadJson = "{\"invitationId\":\"d75e53b9-2701-4cb0-b2d1-c02f0dbf8aa0\",\"invitationCode\":\"ABC123\",\"expiresAt\":\"2026-03-01T10:00:00+00:00\",\"trainerName\":\"Coach\",\"recipientEmail\":\"trainee@example.com\",\"cultureName\":\"en-US\"}"
        };

        var repository = new FakeNotificationRepository { ExistingById = notification };
        var unitOfWork = new FakeUnitOfWork();
        var handler = new InvitationEmailJobHandlerService(
            repository,
            new ThrowingComposer(),
            new FakeEmailSender(),
            unitOfWork,
            NullLogger<InvitationEmailJobHandlerService>.Instance);

        Assert.ThrowsAsync<InvalidOperationException>(() => handler.ProcessAsync(notification.Id));
        Assert.Multiple(() =>
        {
            Assert.That(notification.Status, Is.EqualTo(EmailNotificationStatus.Failed));
            Assert.That(notification.LastError, Does.StartWith("InvalidOperationException"));
            Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task JobHandler_WhenAlreadySent_DoesNothing()
    {
        var notification = new EmailNotificationLog
        {
            Id = Guid.NewGuid(),
            Status = EmailNotificationStatus.Sent,
            Attempts = 2,
            Type = InvitationEmailSchedulerService.NotificationType,
            CorrelationId = Guid.NewGuid(),
            RecipientEmail = "trainee@example.com",
            PayloadJson = "{}"
        };

        var repository = new FakeNotificationRepository { ExistingById = notification };
        var unitOfWork = new FakeUnitOfWork();
        var sender = new FakeEmailSender();
        var handler = new InvitationEmailJobHandlerService(
            repository,
            new PassThroughComposer(),
            sender,
            unitOfWork,
            NullLogger<InvitationEmailJobHandlerService>.Instance);

        await handler.ProcessAsync(notification.Id);

        Assert.Multiple(() =>
        {
            Assert.That(unitOfWork.SaveChangesCalls, Is.EqualTo(0));
            Assert.That(sender.SendCalls, Is.EqualTo(0));
            Assert.That(notification.Attempts, Is.EqualTo(2));
        });
    }

    private sealed class FakeNotificationRepository : IEmailNotificationLogRepository
    {
        public EmailNotificationLog? ExistingById { get; set; }
        public EmailNotificationLog? ExistingByCorrelation { get; set; }
        public EmailNotificationLog? ExistingByCorrelationOnSecondLookup { get; set; }
        public List<EmailNotificationLog> Added { get; } = new();
        private int _correlationLookups;

        public Task AddAsync(EmailNotificationLog log, CancellationToken cancellationToken = default)
        {
            Added.Add(log);
            return Task.CompletedTask;
        }

        public Task<EmailNotificationLog?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingById);
        }

        public Task<EmailNotificationLog?> FindByCorrelationAsync(string type, Guid correlationId, string recipientEmail, CancellationToken cancellationToken = default)
        {
            _correlationLookups += 1;
            if (_correlationLookups >= 2 && ExistingByCorrelationOnSecondLookup != null)
            {
                return Task.FromResult<EmailNotificationLog?>(ExistingByCorrelationOnSecondLookup);
            }

            return Task.FromResult(ExistingByCorrelation);
        }
    }

    private sealed class FakeBackgroundScheduler : IInvitationEmailBackgroundScheduler
    {
        public List<Guid> EnqueuedNotificationIds { get; } = new();

        public void Enqueue(Guid notificationId)
        {
            EnqueuedNotificationIds.Add(notificationId);
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }
        public bool ThrowOnSave { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls += 1;
            if (ThrowOnSave)
            {
                throw new InvalidOperationException("Simulated concurrent insert failure");
            }

            return Task.FromResult(1);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction());
        }
    }

    private sealed class FakeTransaction : IUnitOfWorkTransaction
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ThrowingComposer : IEmailTemplateComposer
    {
        public EmailMessage ComposeTrainerInvitation(InvitationEmailPayload payload)
        {
            throw new InvalidOperationException("Template missing");
        }
    }

    private sealed class PassThroughComposer : IEmailTemplateComposer
    {
        public EmailMessage ComposeTrainerInvitation(InvitationEmailPayload payload)
        {
            return new EmailMessage
            {
                To = payload.RecipientEmail,
                Subject = "x",
                Body = "y"
            };
        }
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public int SendCalls { get; private set; }

        public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            SendCalls += 1;
            return Task.FromResult(true);
        }
    }

    private sealed class EnabledFeature : IEmailNotificationsFeature
    {
        public bool Enabled => true;
    }

    private sealed class DisabledFeature : IEmailNotificationsFeature
    {
        public bool Enabled => false;
    }
}
