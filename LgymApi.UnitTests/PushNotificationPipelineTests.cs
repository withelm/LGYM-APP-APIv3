using System.Net;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Models;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common.Push;
using LgymApi.BackgroundWorker.Common.Push.Models;
using LgymApi.BackgroundWorker.Common.Serialization;
using LgymApi.BackgroundWorker.Push;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Options;
using LgymApi.Infrastructure.Repositories;
using LgymApi.Infrastructure.Services;
using LgymApi.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class PushNotificationPipelineTests
{
    [Test]
    public void PushEventPayload_InAppNotificationId_IsTypedInternally()
    {
        var property = typeof(PushEventPayload).GetProperty(nameof(PushEventPayload.InAppNotificationId));

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(Id<InAppNotification>?));
    }

    [Test]
    public void PushEventPayload_InAppNotificationId_PreservesLegacyUuidStringJson()
    {
        var notificationId = Id<InAppNotification>.New();
        var payload = new PushEventPayload(1, "internal.test.push", "event-1", null, notificationId, null);
        var legacyJson = $$"""
            {"schemaVersion":1,"type":"internal.test.push","eventId":"event-1","entityId":null,"inAppNotificationId":"{{notificationId}}","deeplink":null}
            """;

        var json = JsonSerializer.Serialize(payload, SharedSerializationOptions.Current);
        using var document = JsonDocument.Parse(json);
        var roundtrip = JsonSerializer.Deserialize<PushEventPayload>(json, SharedSerializationOptions.Current);
        var legacyPayload = JsonSerializer.Deserialize<PushEventPayload>(legacyJson, SharedSerializationOptions.Current);

        document.RootElement.GetProperty("inAppNotificationId").GetString().Should().Be(notificationId.ToString());
        roundtrip.Should().NotBeNull();
        roundtrip!.InAppNotificationId.Should().Be(notificationId);
        legacyPayload.Should().NotBeNull();
        legacyPayload!.InAppNotificationId.Should().Be(notificationId);
    }

    [Test]
    public async Task EnqueueAsync_WhenRepeated_DoesNotCreateDuplicateRowsOrReschedule()
    {
        await using var db = CreateDbContext("push-enqueue-duplicate");
        var userId = Id<User>.New();
        var installation = CreateInstallation(userId, permissionStatus: "authorized");
        db.PushInstallations.Add(installation);
        await db.SaveChangesAsync();

        var scheduler = new FakePushBackgroundScheduler();
        var service = new PushNotificationService(
            new PushInstallationRepository(db),
            new PushNotificationMessageRepository(db),
            scheduler,
            new EfUnitOfWork(db),
            NullLogger<PushNotificationService>.Instance);

        var input = new EnqueuePushNotificationInput(
            userId,
            1,
            "trainer.note.updated",
            "event-1",
            "entity-1",
            null,
            "/notifications/event-1");

        await service.EnqueueAsync(input);
        await service.EnqueueAsync(input);

        db.PushNotificationMessages.Should().HaveCount(1);
        db.PushNotificationMessages.Single().Status.Should().Be(PushNotificationStatus.Pending);
        scheduler.EnqueuedNotificationIds.Should().HaveCount(1);
    }

    [Test]
    public async Task EnqueueAsync_WhenExistingPendingMessageHasNoSchedulerJob_ReschedulesExistingMessage()
    {
        await using var db = CreateDbContext("push-enqueue-reschedule-existing");
        var userId = Id<User>.New();
        var installation = CreateInstallation(userId, permissionStatus: "authorized");
        var existing = new PushNotificationMessage
        {
            Id = Id<PushNotificationMessage>.New(),
            UserId = userId,
            PushInstallationId = installation.Id,
            SchemaVersion = 1,
            Type = "trainer.note.updated",
            EventId = "event-1",
            PayloadJson = "{}",
            Status = PushNotificationStatus.Pending,
            SchedulerJobId = null
        };
        db.PushInstallations.Add(installation);
        db.PushNotificationMessages.Add(existing);
        await db.SaveChangesAsync();

        var scheduler = new FakePushBackgroundScheduler();
        var service = new PushNotificationService(
            new PushInstallationRepository(db),
            new PushNotificationMessageRepository(db),
            scheduler,
            new EfUnitOfWork(db),
            NullLogger<PushNotificationService>.Instance);

        await service.EnqueueAsync(new EnqueuePushNotificationInput(
            userId,
            1,
            "trainer.note.updated",
            "event-1",
            "entity-1",
            null,
            "/notifications/event-1"));

        db.PushNotificationMessages.Should().HaveCount(1);
        scheduler.EnqueuedNotificationIds.Should().ContainSingle().Which.Should().Be(existing.Id);
        existing.SchedulerJobId.Should().Be("push-job-id");
    }

    [Test]
    public async Task EnqueueAsync_WhenPermissionDenied_PersistsSkippedMessageWithoutScheduling()
    {
        await using var db = CreateDbContext("push-enqueue-skipped");
        var userId = Id<User>.New();
        db.PushInstallations.Add(CreateInstallation(userId, permissionStatus: "denied"));
        await db.SaveChangesAsync();

        var scheduler = new FakePushBackgroundScheduler();
        var service = new PushNotificationService(
            new PushInstallationRepository(db),
            new PushNotificationMessageRepository(db),
            scheduler,
            new EfUnitOfWork(db),
            NullLogger<PushNotificationService>.Instance);

        await service.EnqueueAsync(new EnqueuePushNotificationInput(
            userId,
            1,
            "trainer.note.updated",
            "event-2",
            null,
            null,
            null));

        db.PushNotificationMessages.Should().HaveCount(1);
        var message = db.PushNotificationMessages.Single();
        message.Status.Should().Be(PushNotificationStatus.Skipped);
        message.FailureKind.Should().Be(PushNotificationFailureKind.Preference);
        scheduler.EnqueuedNotificationIds.Should().BeEmpty();
    }

    [Test]
    public async Task ProcessAsync_WhenInvalidToken_DisablesInstallationWithoutRetry()
    {
        var installation = CreateInstallation(Id<User>.New(), permissionStatus: "authorized");
        var message = CreateMessage(installation.Id);
        var repository = new FakePushNotificationMessageRepository(message);
        var installationRepository = new FakePushInstallationRepository(installation);
        var scheduler = new FakePushBackgroundScheduler();
        var handler = new PushNotificationJobHandlerService(
            repository,
            installationRepository,
            new FakePushProviderSender(new PushSendAttemptResult(PushSendOutcome.InvalidToken, "BadRequest", null, "UNREGISTERED", "registration-token-not-registered")),
            scheduler,
            new FakeUnitOfWork(),
            new PushNotificationOptions(),
            NullLogger<PushNotificationJobHandlerService>.Instance);

        await handler.ProcessAsync(message.Id);

        message.Status.Should().Be(PushNotificationStatus.Failed);
        message.FailureKind.Should().Be(PushNotificationFailureKind.InvalidToken);
        message.NextAttemptAt.Should().BeNull();
        installation.DisabledReason.Should().Be("InvalidFcmToken");
        installation.DisabledAt.Should().NotBeNull();
        scheduler.ScheduledRetries.Should().BeEmpty();
    }

    [Test]
    public async Task ProcessAsync_WhenTransientFailure_SchedulesRetry()
    {
        var installation = CreateInstallation(Id<User>.New(), permissionStatus: "authorized");
        var message = CreateMessage(installation.Id);
        var repository = new FakePushNotificationMessageRepository(message);
        var scheduler = new FakePushBackgroundScheduler();
        var handler = new PushNotificationJobHandlerService(
            repository,
            new FakePushInstallationRepository(installation),
            new FakePushProviderSender(new PushSendAttemptResult(PushSendOutcome.TransientFailure, "ServiceUnavailable", null, "UNAVAILABLE", "temporary outage")),
            scheduler,
            new FakeUnitOfWork(),
            new PushNotificationOptions { RetryDelaysSeconds = [5] },
            NullLogger<PushNotificationJobHandlerService>.Instance);

        await handler.ProcessAsync(message.Id);

        message.Status.Should().Be(PushNotificationStatus.Failed);
        message.FailureKind.Should().Be(PushNotificationFailureKind.Transient);
        message.NextAttemptAt.Should().NotBeNull();
        scheduler.ScheduledRetries.Should().ContainSingle();
        scheduler.ScheduledRetries[0].notificationId.Should().Be(message.Id);
        scheduler.ScheduledRetries[0].delay.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task ProcessAsync_WhenClaimFails_DoesNotSendAgain()
    {
        var installation = CreateInstallation(Id<User>.New(), permissionStatus: "authorized");
        var message = CreateMessage(installation.Id);
        var repository = new FakePushNotificationMessageRepository(message) { TryClaimResult = false };
        var sender = new FakePushProviderSender(new PushSendAttemptResult(PushSendOutcome.Sent, "OK", "message-1", null, "ok"));
        var handler = new PushNotificationJobHandlerService(
            repository,
            new FakePushInstallationRepository(installation),
            sender,
            new FakePushBackgroundScheduler(),
            new FakeUnitOfWork(),
            new PushNotificationOptions(),
            NullLogger<PushNotificationJobHandlerService>.Instance);

        await handler.ProcessAsync(message.Id);

        sender.SendCalls.Should().Be(0);
        message.Attempts.Should().Be(0);
    }

    [Test]
    public async Task CleanupAsync_WhenInstallationIsStale_DisablesItWithoutDeletingAuditState()
    {
        var staleInstallation = CreateInstallation(Id<User>.New(), permissionStatus: "authorized");
        staleInstallation.LastSeenAt = DateTimeOffset.UtcNow.AddDays(-60);
        var recentInstallation = CreateInstallation(Id<User>.New(), permissionStatus: "authorized");
        recentInstallation.LastSeenAt = DateTimeOffset.UtcNow.AddDays(-5);

        var repository = new FakePushInstallationRepository(staleInstallation, recentInstallation);
        var unitOfWork = new FakeUnitOfWork();
        var service = new StalePushInstallationCleanupService(
            repository,
            unitOfWork,
            new FakeStalePushInstallationCleanupSettings
            {
                Enabled = true,
                InactivityDays = 30,
                BatchSize = 10
            },
            NullLogger<StalePushInstallationCleanupService>.Instance);

        var cleaned = await service.CleanupAsync();

        cleaned.Should().Be(1);
        staleInstallation.DisabledReason.Should().Be(StalePushInstallationCleanupService.StaleInstallationDisabledReason);
        staleInstallation.DisabledAt.Should().NotBeNull();
        recentInstallation.DisabledAt.Should().BeNull();
        unitOfWork.SaveChangesCalls.Should().Be(1);
    }

    [Test]
    public async Task FcmSender_BuildsPrivacySafeNotificationPayloadForAndroidDisplay()
    {
        var sender = new FcmPushSender(
            new FakeHttpClientFactory(),
            new PushNotificationOptions(),
            NullLogger<FcmPushSender>.Instance);
        var installation = CreateInstallation(Id<User>.New(), permissionStatus: "authorized");
        var notificationId = Id<InAppNotification>.New();
        var payload = new PushEventPayload(
            1,
            "internal.test.push",
            "event-1",
            "entity-1",
            notificationId,
            "/notifications");

        var method = typeof(FcmPushSender).GetMethod("BuildRequestContent", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        using var content = (StringContent)method!.Invoke(sender, [installation, payload])!;
        var json = await content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var message = document.RootElement.GetProperty("message");

        message.GetProperty("notification").GetProperty("title").GetString().Should().Be("LGYM");
        message.GetProperty("notification").GetProperty("body").GetString().Should().Be("You have a new notification.");
        message.GetProperty("android").GetProperty("priority").GetString().Should().Be("HIGH");
        message.GetProperty("data").GetProperty("deeplink").GetString().Should().Be("/notifications");
        message.GetProperty("data").GetProperty("inAppNotificationId").GetString().Should().Be(notificationId.ToString());
    }

    [Test]
    public async Task FcmSender_WhenSendingDisabled_ReturnsSkippedWithoutHttpCall()
    {
        var httpClientFactory = new FakeHttpClientFactory();
        var sender = new FcmPushSender(
            httpClientFactory,
            new PushNotificationOptions { SendEnabled = false },
            NullLogger<FcmPushSender>.Instance);
        var installation = CreateInstallation(Id<User>.New(), permissionStatus: "authorized");
        var payload = new PushEventPayload(1, "internal.test.push", "event-1", null, null, null);

        var result = await sender.SendAsync(installation, payload);

        result.Outcome.Should().Be(PushSendOutcome.Skipped);
        result.ProviderStatus.Should().Be("Skipped");
        result.ProviderErrorCode.Should().Be("push-disabled");
        httpClientFactory.CreateClientCalls.Should().Be(0);
    }

    [Test]
    public void FcmSender_BuildsProjectScopedSendUrl()
    {
        var sender = new FcmPushSender(
            new FakeHttpClientFactory(),
            new PushNotificationOptions
            {
                Fcm = { BaseUrl = "https://fcm.example.test", ProjectId = "lgym-test" }
            },
            NullLogger<FcmPushSender>.Instance);

        var url = InvokePrivate<string>(sender, "BuildSendUrl");

        url.Should().Be("https://fcm.example.test/v1/projects/lgym-test/messages:send");
    }

    [TestCase(HttpStatusCode.BadRequest, "registration-token-not-registered", PushSendOutcome.InvalidToken)]
    [TestCase(HttpStatusCode.ServiceUnavailable, "provider unavailable", PushSendOutcome.TransientFailure)]
    [TestCase(HttpStatusCode.BadGateway, "bad gateway", PushSendOutcome.TransientFailure)]
    [TestCase(HttpStatusCode.BadRequest, "invalid payload", PushSendOutcome.PermanentFailure)]
    public void FcmSender_ClassifiesProviderFailures(HttpStatusCode statusCode, string summary, PushSendOutcome expectedOutcome)
    {
        var outcome = InvokeStaticPrivate<PushSendOutcome>(
            typeof(FcmPushSender),
            "ClassifyFailure",
            statusCode,
            summary);

        outcome.Should().Be(expectedOutcome);
    }

    [Test]
    public void FcmSender_ExtractsProviderValuesFromDirectAndNestedJson()
    {
        InvokeStaticPrivate<string?>(typeof(FcmPushSender), "TryExtractValue", "{\"name\":\"projects/lgym/messages/1\"}", "name")
            .Should().Be("projects/lgym/messages/1");
        InvokeStaticPrivate<string?>(typeof(FcmPushSender), "TryExtractValue", "{\"error\":{\"status\":\"UNREGISTERED\"}}", "status")
            .Should().Be("UNREGISTERED");
        InvokeStaticPrivate<string?>(typeof(FcmPushSender), "TryExtractValue", "not-json", "status")
            .Should().BeNull();
        InvokeStaticPrivate<string?>(typeof(FcmPushSender), "TryExtractValue", string.Empty, "status")
            .Should().BeNull();
    }

    [Test]
    public void FcmSender_SummarizesProviderResponses()
    {
        InvokeStaticPrivate<string>(typeof(FcmPushSender), "Summarize", " first\r\nsecond ")
            .Should().Be("first  second");
        InvokeStaticPrivate<string>(typeof(FcmPushSender), "Summarize", new string('x', 1005))
            .Should().HaveLength(1000);
        InvokeStaticPrivate<string>(typeof(FcmPushSender), "Summarize", (object?)null)
            .Should().BeEmpty();
    }

    private static AppDbContext CreateDbContext(string name)
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"{name}-{Id<PushNotificationPipelineTests>.New():N}")
            .Options);

    private static PushInstallation CreateInstallation(Id<User> userId, string permissionStatus)
        => new()
        {
            Id = Id<PushInstallation>.New(),
            UserId = userId,
            InstallationId = $"device-{Id<PushInstallation>.New():N}",
            Platform = "android",
            FcmToken = "token-1",
            Environment = "development",
            PermissionStatus = permissionStatus,
            LastSeenAt = DateTimeOffset.UtcNow
        };

    private static PushNotificationMessage CreateMessage(Id<PushInstallation> installationId)
        => new()
        {
            Id = Id<PushNotificationMessage>.New(),
            UserId = Id<User>.New(),
            PushInstallationId = installationId,
            SchemaVersion = 1,
            Type = "trainer.note.updated",
            EventId = "event-1",
            PayloadJson = "{\"schemaVersion\":1,\"type\":\"trainer.note.updated\",\"eventId\":\"event-1\"}",
            Status = PushNotificationStatus.Pending
        };

    private static TResult InvokePrivate<TResult>(object instance, string methodName, params object?[] parameters)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        return (TResult)method!.Invoke(instance, parameters)!;
    }

    private static TResult InvokeStaticPrivate<TResult>(Type type, string methodName, params object?[] parameters)
    {
        var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        return (TResult)method!.Invoke(null, parameters)!;
    }

    private sealed class FakePushBackgroundScheduler : IPushBackgroundScheduler
    {
        public List<Id<PushNotificationMessage>> EnqueuedNotificationIds { get; } = [];
        public List<(Id<PushNotificationMessage> notificationId, TimeSpan delay)> ScheduledRetries { get; } = [];

        public string? Enqueue(Id<PushNotificationMessage> notificationId)
        {
            EnqueuedNotificationIds.Add(notificationId);
            return "push-job-id";
        }

        public string? ScheduleRetry(Id<PushNotificationMessage> notificationId, TimeSpan delay)
        {
            ScheduledRetries.Add((notificationId, delay));
            return "push-retry-job-id";
        }
    }

    private sealed class FakePushProviderSender : IPushProviderSender
    {
        private readonly PushSendAttemptResult _result;

        public FakePushProviderSender(PushSendAttemptResult result)
        {
            _result = result;
        }

        public int SendCalls { get; private set; }

        public Task<PushSendAttemptResult> SendAsync(PushInstallation installation, PushEventPayload payload, CancellationToken cancellationToken = default)
        {
            SendCalls += 1;
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public int CreateClientCalls { get; private set; }

        public HttpClient CreateClient(string name)
        {
            CreateClientCalls += 1;
            return new HttpClient();
        }
    }

    private sealed class FakePushNotificationMessageRepository : IPushNotificationMessageRepository
    {
        private readonly PushNotificationMessage _message;

        public FakePushNotificationMessageRepository(PushNotificationMessage message)
        {
            _message = message;
        }

        public bool TryClaimResult { get; set; } = true;

        public Task AddAsync(PushNotificationMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<PushNotificationMessage?> FindByIdAsync(Id<PushNotificationMessage> id, CancellationToken cancellationToken = default)
            => Task.FromResult<PushNotificationMessage?>(_message.Id == id ? _message : null);

        public Task<PushNotificationMessage?> FindByDeliveryKeyAsync(Id<PushInstallation> installationId, string type, string eventId, CancellationToken cancellationToken = default)
            => Task.FromResult<PushNotificationMessage?>(null);

        public Task<bool> TryTransitionToSendingAsync(Id<PushNotificationMessage> id, CancellationToken cancellationToken = default)
            => Task.FromResult(TryClaimResult);

        public Task UpdateAsync(PushNotificationMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<List<PushNotificationMessage>> GetByStatusAsync(PushNotificationStatus status, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<PushNotificationMessage>());
    }

    private sealed class FakePushInstallationRepository : IPushInstallationRepository
    {
        private readonly List<PushInstallation> _installations;

        public FakePushInstallationRepository(params PushInstallation[] installations)
        {
            _installations = installations.ToList();
        }

        public Task<PushInstallation?> FindByIdAsync(Id<PushInstallation> id, CancellationToken cancellationToken = default)
            => Task.FromResult<PushInstallation?>(_installations.FirstOrDefault(x => x.Id == id));

        public Task<PushInstallation?> FindByInstallationIdAsync(string installationId, CancellationToken cancellationToken = default)
            => Task.FromResult<PushInstallation?>(_installations.FirstOrDefault(x => x.InstallationId == installationId));

        public Task<PushInstallation?> FindBoundToUserOrSessionAsync(string installationId, Id<User> userId, Id<UserSession> sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult<PushInstallation?>(_installations.FirstOrDefault(x => x.InstallationId == installationId && (x.UserId == userId || x.SessionId == sessionId)));

        public Task<List<PushInstallation>> GetActiveByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default)
            => Task.FromResult(_installations.Where(x => x.UserId == userId && x.DisabledAt == null).ToList());

        public Task<List<PushInstallation>> GetBySessionIdAsync(Id<UserSession> sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(_installations.Where(x => x.SessionId == sessionId).ToList());

        public Task<List<PushInstallation>> GetStaleActiveAsync(DateTimeOffset lastSeenBefore, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult(_installations.Where(x => x.DisabledAt == null && x.LastSeenAt < lastSeenBefore).OrderBy(x => x.LastSeenAt).Take(limit).ToList());

        public Task AddAsync(PushInstallation installation, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(PushInstallation installation, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpsertForUserSessionAsync(PushInstallationRegistration registration, CancellationToken cancellationToken = default)
        {
            var installation = _installations.FirstOrDefault(entity => entity.InstallationId == registration.InstallationId);
            if (installation == null)
            {
                installation = new PushInstallation { Id = Id<PushInstallation>.New() };
                _installations.Add(installation);
            }

            installation.UserId = registration.UserId;
            installation.SessionId = registration.SessionId;
            installation.InstallationId = registration.InstallationId;
            installation.Platform = registration.Platform;
            installation.FcmToken = registration.FcmToken;
            installation.AppVersion = registration.AppVersion;
            installation.Environment = registration.Environment;
            installation.PermissionStatus = registration.PermissionStatus;
            installation.LastSeenAt = registration.LastSeenAt;
            installation.DisabledAt = null;
            installation.DisabledReason = null;
            return Task.CompletedTask;
        }

        public Task<bool> DisableBoundForUserOrSessionAsync(
            string installationId,
            Id<User> userId,
            Id<UserSession> sessionId,
            DateTimeOffset disabledAt,
            string disabledReason,
            CancellationToken cancellationToken = default)
        {
            var installation = _installations.FirstOrDefault(entity => entity.InstallationId == installationId && (entity.UserId == userId || entity.SessionId == sessionId));
            if (installation == null)
            {
                return Task.FromResult(false);
            }

            installation.DisabledAt = disabledAt;
            installation.DisabledReason = disabledReason;
            installation.LastSeenAt = disabledAt;
            return Task.FromResult(true);
        }

        public Task<bool> DisassociateBoundForUserOrSessionAsync(
            string installationId,
            Id<User> userId,
            Id<UserSession> sessionId,
            DateTimeOffset lastSeenAt,
            CancellationToken cancellationToken = default)
        {
            var installation = _installations.FirstOrDefault(entity => entity.InstallationId == installationId && (entity.UserId == userId || entity.SessionId == sessionId));
            if (installation == null)
            {
                return Task.FromResult(false);
            }

            installation.UserId = null;
            installation.SessionId = null;
            installation.LastSeenAt = lastSeenAt;
            return Task.FromResult(true);
        }

        public Task DisassociateForSessionAsync(Id<UserSession> sessionId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken = default)
        {
            foreach (var installation in _installations.Where(entity => entity.SessionId == sessionId))
            {
                installation.UserId = null;
                installation.SessionId = null;
                installation.LastSeenAt = lastSeenAt;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls += 1;
            return Task.FromResult(1);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IUnitOfWorkTransaction>(new FakeUnitOfWorkTransaction());
    }

    private sealed class FakeUnitOfWorkTransaction : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeStalePushInstallationCleanupSettings : IStalePushInstallationCleanupSettings
    {
        public bool Enabled { get; init; }
        public int InactivityDays { get; init; }
        public int BatchSize { get; init; }
    }
}
