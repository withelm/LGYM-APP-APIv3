using FluentAssertions;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Contracts.Push;
using LgymApi.BackgroundWorker.Push;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class PushNotificationPipelineIntegrationTests
{
    [Test]
    public async Task EnqueueAsync_PersistsPendingAndSkippedRows_ByInstallationEligibility()
    {
        await using var factory = new CustomWebApplicationFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await TestUtils.TestDataFactory.SeedUserAsync(db, "push-int-user", "push-int-user@example.com", "pass123");
            db.PushInstallations.AddRange(
                new PushInstallation
                {
                    Id = Id<PushInstallation>.New(),
                    UserId = user.Id,
                    InstallationId = "device-active",
                    Platform = "android",
                    FcmToken = "token-active",
                    Environment = "development",
                    PermissionStatus = "authorized",
                    LastSeenAt = DateTimeOffset.UtcNow
                },
                new PushInstallation
                {
                    Id = Id<PushInstallation>.New(),
                    UserId = user.Id,
                    InstallationId = "device-denied",
                    Platform = "ios",
                    FcmToken = "token-denied",
                    Environment = "development",
                    PermissionStatus = "denied",
                    LastSeenAt = DateTimeOffset.UtcNow
                });
            await db.SaveChangesAsync();

            var service = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
            await service.EnqueueAsync(new Application.Notifications.Models.EnqueuePushNotificationInput(
                user.Id,
                1,
                "trainer.note.updated",
                "event-integration-1",
                null,
                null,
                null));
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = db.PushNotificationMessages.OrderBy(x => x.EventId).ToList();
            rows.Should().HaveCount(2);
            rows.Should().ContainSingle(x => x.Status == PushNotificationStatus.Pending);
            rows.Should().ContainSingle(x => x.Status == PushNotificationStatus.Skipped && x.FailureKind == PushNotificationFailureKind.Preference);
        }
    }

    [Test]
    public async Task ProcessAsync_WithInjectedInvalidTokenSender_DisablesInstallationAndPersistsFailure()
    {
        var fakeSender = new InvalidTokenPushProviderSender();
        using var baseFactory = new CustomWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPushProviderSender>();
                services.AddSingleton<IPushProviderSender>(fakeSender);
            });
        });

        Id<PushNotificationMessage> notificationId;
        Id<PushInstallation> installationId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await TestUtils.TestDataFactory.SeedUserAsync(db, "push-int-send", "push-int-send@example.com", "pass123");
            var session = CreateActiveSession(user.Id);
            var installation = new PushInstallation
            {
                Id = Id<PushInstallation>.New(),
                UserId = user.Id,
                SessionId = session.Id,
                InstallationId = "device-invalid",
                Platform = "android",
                FcmToken = "token-invalid",
                Environment = "development",
                PermissionStatus = "authorized",
                LastSeenAt = DateTimeOffset.UtcNow
            };
            db.UserSessions.Add(session);
            db.PushInstallations.Add(installation);
            await db.SaveChangesAsync();
            installationId = installation.Id;

            var service = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
            await service.EnqueueAsync(new Application.Notifications.Models.EnqueuePushNotificationInput(
                user.Id,
                1,
                "trainer.note.updated",
                "event-integration-2",
                null,
                null,
                null));

            notificationId = db.PushNotificationMessages.Single().Id;
        }

        using (var scope = factory.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<PushNotificationJobHandlerService>();
            await handler.ProcessAsync(notificationId.ToString());
        }

        fakeSender.LastInstallationId.Should().Be(installationId);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var installation = db.PushInstallations.Single(x => x.InstallationId == "device-invalid");
            var message = db.PushNotificationMessages.Single(x => x.Id == notificationId);

            installation.DisabledReason.Should().Be("InvalidFcmToken");
            message.Status.Should().Be(PushNotificationStatus.Failed);
            message.FailureKind.Should().Be(PushNotificationFailureKind.InvalidToken);
            message.NextAttemptAt.Should().BeNull();
        }
    }

    [Test]
    public async Task ProcessAsync_WhenInstallationIsDisabledAfterQueueing_SuppressesProviderDelivery()
    {
        var sender = new CountingPushProviderSender();
        using var baseFactory = new CustomWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPushProviderSender>();
                services.AddSingleton<IPushProviderSender>(sender);
            });
        });

        Id<PushNotificationMessage> notificationId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await TestUtils.TestDataFactory.SeedUserAsync(db, "push-disabled-after-queue", "push-disabled-after-queue@example.com", "pass123");
            var session = CreateActiveSession(user.Id);
            var installation = new PushInstallation
            {
                Id = Id<PushInstallation>.New(),
                UserId = user.Id,
                SessionId = session.Id,
                InstallationId = "device-disabled-after-queue",
                Platform = "android",
                FcmToken = "token-disabled-after-queue",
                Environment = "development",
                PermissionStatus = "authorized",
                LastSeenAt = DateTimeOffset.UtcNow
            };
            db.UserSessions.Add(session);
            db.PushInstallations.Add(installation);
            await db.SaveChangesAsync();

            var enqueue = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
            await enqueue.EnqueueAsync(new Application.Notifications.Models.EnqueuePushNotificationInput(
                user.Id,
                1,
                "trainer.note.updated",
                "event-disabled-after-queue",
                null,
                null,
                null));
            notificationId = db.PushNotificationMessages.Single().Id;
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var installation = db.PushInstallations.Single(candidate => candidate.InstallationId == "device-disabled-after-queue");
            installation.DisabledAt = DateTimeOffset.UtcNow;
            installation.DisabledReason = "InactiveStale";
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<PushNotificationJobHandlerService>();
            await handler.ProcessAsync(notificationId.ToString());
        }

        sender.SendCalls.Should().Be(0);
        using var assertionScope = factory.Services.CreateScope();
        var assertionDatabase = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var message = assertionDatabase.PushNotificationMessages.Single(candidate => candidate.Id == notificationId);
        message.Status.Should().Be(PushNotificationStatus.Skipped);
        message.FailureKind.Should().Be(PushNotificationFailureKind.Preference);
        message.ProviderStatus.Should().Be("InstallationInactive");
        message.NextAttemptAt.Should().BeNull();
    }

    [Test]
    public async Task ProcessAsync_RevalidatesCurrentSourceAndSuppressesOnlyTheHiddenNotification()
    {
        var sender = new CountingPushProviderSender();
        using var baseFactory = new CustomWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPushProviderSender>();
                services.AddSingleton<IPushProviderSender>(sender);
            });
        });

        Id<PushNotificationMessage> validMessageId;
        Id<PushNotificationMessage> hiddenMessageId;
        Id<TraineeNote> hiddenNoteId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var trainee = await TestUtils.TestDataFactory.SeedUserAsync(db, "push-source-trainee", "push-source-trainee@example.com", "pass123");
            var trainer = await TestUtils.TestDataFactory.SeedUserAsync(db, "push-source-trainer", "push-source-trainer@example.com", "pass123");
            var session = CreateActiveSession(trainee.Id);
            var installation = new PushInstallation
            {
                Id = Id<PushInstallation>.New(),
                UserId = trainee.Id,
                SessionId = session.Id,
                InstallationId = "device-source-privacy",
                Platform = "android",
                FcmToken = "token-source-privacy",
                Environment = "development",
                PermissionStatus = "authorized",
                LastSeenAt = DateTimeOffset.UtcNow
            };
            var validNote = CreateVisibleNote(trainer.Id, trainee.Id, "valid note");
            var hiddenNote = CreateVisibleNote(trainer.Id, trainee.Id, "hidden later");
            var validNotification = CreateNoteNotification(validNote, trainer.Id, trainee.Id);
            var hiddenNotification = CreateNoteNotification(hiddenNote, trainer.Id, trainee.Id);

            db.UserSessions.Add(session);
            db.PushInstallations.Add(installation);
            db.TraineeNotes.AddRange(validNote, hiddenNote);
            db.InAppNotifications.AddRange(validNotification, hiddenNotification);
            await db.SaveChangesAsync();

            var enqueue = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
            await enqueue.EnqueueAsync(new Application.Notifications.Models.EnqueuePushNotificationInput(
                trainee.Id, 1, validNotification.Type.Value, validNotification.Id.ToString(), null, validNotification.Id, validNotification.RedirectUrl));
            await enqueue.EnqueueAsync(new Application.Notifications.Models.EnqueuePushNotificationInput(
                trainee.Id, 1, hiddenNotification.Type.Value, hiddenNotification.Id.ToString(), null, hiddenNotification.Id, hiddenNotification.RedirectUrl));

            validMessageId = db.PushNotificationMessages.Single(message => message.InAppNotificationId == validNotification.Id).Id;
            hiddenMessageId = db.PushNotificationMessages.Single(message => message.InAppNotificationId == hiddenNotification.Id).Id;
            hiddenNoteId = hiddenNote.Id;
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TraineeNotes.Single(note => note.Id == hiddenNoteId).VisibleToTrainee = false;
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<PushNotificationJobHandlerService>();
            await handler.ProcessAsync(validMessageId.ToString());
            await handler.ProcessAsync(hiddenMessageId.ToString());
        }

        sender.SendCalls.Should().Be(1);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.PushNotificationMessages.Single(message => message.Id == validMessageId).Status.Should().Be(PushNotificationStatus.Sent);
            var hiddenMessage = db.PushNotificationMessages.Single(message => message.Id == hiddenMessageId);
            hiddenMessage.Status.Should().Be(PushNotificationStatus.Skipped);
            hiddenMessage.ProviderStatus.Should().Be("SourceNotificationUnavailable");
        }
    }

    [Test]
    public async Task EnqueueAsync_WhenConcurrentForSameDeliveryKey_PersistsOneMessageAndSendsOnce()
    {
        var sender = new CountingPushProviderSender();
        using var baseFactory = new CustomWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPushProviderSender>();
                services.AddSingleton<IPushProviderSender>(sender);
            });
        });

        Id<User> userId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await TestUtils.TestDataFactory.SeedUserAsync(db, "push-dedup-user", "push-dedup@example.com", "pass123");
            userId = user.Id;
            var session = CreateActiveSession(userId);
            db.UserSessions.Add(session);
            db.PushInstallations.Add(new PushInstallation
            {
                Id = Id<PushInstallation>.New(),
                UserId = userId,
                SessionId = session.Id,
                InstallationId = "device-dedup",
                Platform = "android",
                FcmToken = "token-dedup",
                Environment = "development",
                PermissionStatus = "authorized",
                LastSeenAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var input = new Application.Notifications.Models.EnqueuePushNotificationInput(
            userId,
            1,
            "trainer.note.updated",
            "event-integration-dedup",
            null,
            null,
            null);

        using (var firstScope = factory.Services.CreateScope())
        using (var secondScope = factory.Services.CreateScope())
        {
            var firstService = firstScope.ServiceProvider.GetRequiredService<IPushNotificationService>();
            var secondService = secondScope.ServiceProvider.GetRequiredService<IPushNotificationService>();

            await Task.WhenAll(
                firstService.EnqueueAsync(input),
                secondService.EnqueueAsync(input));
        }

        Id<PushNotificationMessage> messageId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = db.PushNotificationMessages
                .Where(x => x.EventId == "event-integration-dedup")
                .ToList();
            rows.Should().ContainSingle();
            rows[0].Status.Should().Be(PushNotificationStatus.Pending);
            messageId = rows[0].Id;

            var deliveryKeyIndex = db.Model.FindEntityType(typeof(PushNotificationMessage))!
                .GetIndexes()
                .Single(index => index.Properties.Select(property => property.Name)
                    .SequenceEqual(["PushInstallationId", "Type", "EventId"]));
            deliveryKeyIndex.IsUnique.Should().BeTrue();
        }

        using (var firstScope = factory.Services.CreateScope())
        using (var secondScope = factory.Services.CreateScope())
        {
            var firstHandler = firstScope.ServiceProvider.GetRequiredService<PushNotificationJobHandlerService>();
            var secondHandler = secondScope.ServiceProvider.GetRequiredService<PushNotificationJobHandlerService>();

            await firstHandler.ProcessAsync(messageId.ToString());
            await secondHandler.ProcessAsync(messageId.ToString());
        }

        sender.SendCalls.Should().Be(1);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.PushNotificationMessages.Single(x => x.Id == messageId).Status.Should().Be(PushNotificationStatus.Sent);
        }
    }

    [Test]
    public async Task CleanupAsync_TombstonesStaleInstallationWithoutDeletingAuditRowsOrEnqueueingToIt()
    {
        using var baseFactory = new CustomWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("PushNotifications:StaleTokenCleanupEnabled", "true");
            builder.UseSetting("PushNotifications:StaleTokenInactivityDays", "30");
            builder.UseSetting("PushNotifications:StaleTokenCleanupBatchSize", "10");
        });

        Id<User> userId;
        Id<PushInstallation> staleInstallationId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await TestUtils.TestDataFactory.SeedUserAsync(db, "push-stale-user", "push-stale@example.com", "pass123");
            userId = user.Id;
            var staleInstallation = new PushInstallation
            {
                Id = Id<PushInstallation>.New(),
                UserId = userId,
                InstallationId = "device-stale",
                Platform = "android",
                FcmToken = "token-stale",
                Environment = "development",
                PermissionStatus = "authorized",
                LastSeenAt = DateTimeOffset.UtcNow.AddDays(-31)
            };
            staleInstallationId = staleInstallation.Id;
            db.PushInstallations.AddRange(
                staleInstallation,
                new PushInstallation
                {
                    Id = Id<PushInstallation>.New(),
                    UserId = userId,
                    InstallationId = "device-current",
                    Platform = "ios",
                    FcmToken = "token-current",
                    Environment = "development",
                    PermissionStatus = "authorized",
                    LastSeenAt = DateTimeOffset.UtcNow
                });
            db.PushNotificationMessages.Add(new PushNotificationMessage
            {
                Id = Id<PushNotificationMessage>.New(),
                UserId = userId,
                PushInstallationId = staleInstallationId,
                SchemaVersion = 1,
                Type = "trainer.note.updated",
                EventId = "event-stale-audit",
                PayloadJson = "{}",
                Status = PushNotificationStatus.Sent,
                FailureKind = PushNotificationFailureKind.None,
                SentAt = DateTimeOffset.UtcNow.AddDays(-31)
            });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var cleanup = scope.ServiceProvider.GetRequiredService<IStalePushInstallationCleanupService>();
            (await cleanup.CleanupAsync()).Should().Be(1);

            var enqueue = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
            await enqueue.EnqueueAsync(new Application.Notifications.Models.EnqueuePushNotificationInput(
                userId,
                1,
                "trainer.note.updated",
                "event-after-stale-cleanup",
                null,
                null,
                null));
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var staleInstallation = db.PushInstallations.Single(x => x.Id == staleInstallationId);
            staleInstallation.DisabledAt.Should().NotBeNull();
            staleInstallation.DisabledReason.Should().Be("InactiveStale");
            db.PushInstallations.Count().Should().Be(2);
            db.PushNotificationMessages.Single(x => x.EventId == "event-stale-audit").PushInstallationId.Should().Be(staleInstallationId);
            db.PushNotificationMessages.Count(x => x.EventId == "event-after-stale-cleanup" && x.PushInstallationId == staleInstallationId)
                .Should().Be(0);
            db.PushNotificationMessages.Count(x => x.EventId == "event-after-stale-cleanup").Should().Be(1);
        }
    }

    private sealed class InvalidTokenPushProviderSender : IPushProviderSender
    {
        public Id<PushInstallation>? LastInstallationId { get; private set; }

        public Task<PushSendAttemptResult> SendAsync(Id<PushInstallation> installationId, PushEventPayload payload, CancellationToken cancellationToken = default)
        {
            LastInstallationId = installationId;
            return Task.FromResult(new PushSendAttemptResult(
                PushSendOutcome.InvalidToken,
                "BadRequest",
                null,
                "UNREGISTERED",
                "registration-token-not-registered"));
        }
    }

    private static UserSession CreateActiveSession(Id<User> userId) => new()
    {
        Id = Id<UserSession>.New(),
        UserId = userId,
        Jti = Id<UserSession>.New().ToString(),
        ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
    };

    private static TraineeNote CreateVisibleNote(Id<User> trainerId, Id<User> traineeId, string title)
        => new()
        {
            Id = Id<TraineeNote>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            Title = title,
            Content = title,
            VisibleToTrainee = true,
            LastUpdatedByUserId = trainerId,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

    private static InAppNotification CreateNoteNotification(TraineeNote note, Id<User> trainerId, Id<User> traineeId)
        => new()
        {
            Id = Id<InAppNotification>.New(),
            RecipientId = traineeId,
            SenderUserId = trainerId,
            DeliveryKey = $"trainee-note:{note.Id}:{note.LastUpdatedAt:O}",
            Message = note.Title!,
            RedirectUrl = $"/trainer/notes/{note.Id}",
            Type = LgymApi.Domain.Notifications.InAppNotificationTypes.TraineeNoteUpdated
        };

    private sealed class CountingPushProviderSender : IPushProviderSender
    {
        public int SendCalls { get; private set; }

        public Task<PushSendAttemptResult> SendAsync(Id<PushInstallation> installationId, PushEventPayload payload, CancellationToken cancellationToken = default)
        {
            SendCalls += 1;
            return Task.FromResult(new PushSendAttemptResult(
                PushSendOutcome.Sent,
                "OK",
                "provider-message-id",
                null,
                null));
        }
    }
}
