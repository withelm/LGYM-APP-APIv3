using FluentAssertions;
using LgymApi.Application.Notifications;
using LgymApi.Application.Notifications.Contracts.Push;
using LgymApi.Application.Notifications.Repositories;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LgymApi.Notifications.UnitTests;

[TestFixture]
public sealed class PushNotificationDeliveryRecoveryTests
{
    [Test]
    public async Task ProcessAsync_WhenInvalidTokenMutationConflicts_ResetsInstallationBeforeUncancelledRecoverySave()
    {
        var userId = Id<User>.New();
        var originalSessionId = Id<UserSession>.New();
        var replacementSessionId = Id<UserSession>.New();
        var installation = new PushInstallation
        {
            Id = Id<PushInstallation>.New(),
            UserId = userId,
            SessionId = originalSessionId,
            InstallationId = "delivery-recovery-device",
            Platform = "android",
            FcmToken = "token-a",
            Environment = "testing",
            LastSeenAt = DateTimeOffset.UtcNow
        };
        var message = new PushNotificationMessage
        {
            Id = Id<PushNotificationMessage>.New(),
            UserId = userId,
            PushInstallationId = installation.Id,
            SchemaVersion = 1,
            Type = "trainer.note.updated",
            EventId = "delivery-recovery-event",
            PayloadJson = """{"schemaVersion":1,"type":"trainer.note.updated","eventId":"delivery-recovery-event"}""",
            Status = PushNotificationStatus.Pending
        };
        var installationRepository = new RecoveryInstallationRepository(installation, replacementSessionId);
        var unitOfWork = new RecoveryUnitOfWork(installationRepository);
        var service = new PushNotificationDeliveryService(
            new RecoveryMessageRepository(message),
            new AllowAllDisclosureEligibility(),
            installationRepository,
            new InvalidTokenSender(),
            new NoOpPushScheduler(),
            new NoRetrySettings(),
            unitOfWork,
            new ActiveAccountResolver(),
            NullLogger<PushNotificationDeliveryService>.Instance);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        Func<Task> act = () => service.ProcessAsync(message.Id.ToString(), cancellationTokenSource.Token);

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        installationRepository.ResetCancellationToken.CanBeCanceled.Should().BeFalse();
        unitOfWork.RecoverySaveCancellationToken.CanBeCanceled.Should().BeFalse();
        unitOfWork.RecoverySaveObservedAfterReset.Should().BeTrue();
        installation.FcmToken.Should().Be("token-b");
        installation.SessionId.Should().Be(replacementSessionId);
        installation.RegistrationRevision.Should().Be(1);
        installation.DisabledAt.Should().BeNull();
        installation.DisabledReason.Should().BeNull();
        message.Status.Should().Be(PushNotificationStatus.Failed);
        message.FailureKind.Should().Be(PushNotificationFailureKind.Transient);
    }

    private sealed class RecoveryMessageRepository(PushNotificationMessage message) : IPushNotificationMessageRepository
    {
        public Task AddAsync(PushNotificationMessage item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Detach(PushNotificationMessage item) { }
        public Task<PushNotificationMessage?> FindByIdAsync(Id<PushNotificationMessage> id, CancellationToken cancellationToken = default)
            => Task.FromResult<PushNotificationMessage?>(id == message.Id ? message : null);
        public Task<PushNotificationMessage?> FindByDeliveryKeyAsync(Id<PushInstallation> installationId, string type, string eventId, CancellationToken cancellationToken = default)
            => Task.FromResult<PushNotificationMessage?>(null);
        public Task<bool> TryReserveSchedulingAsync(Id<PushNotificationMessage> id, string reservationId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task ClearSchedulingReservationAsync(Id<PushNotificationMessage> id, string reservationId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<bool> TryTransitionToSendingAsync(Id<PushNotificationMessage> id, CancellationToken cancellationToken = default)
        {
            message.Status = PushNotificationStatus.Sending;
            return Task.FromResult(id == message.Id);
        }
        public Task UpdateAsync(PushNotificationMessage item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<PushNotificationMessage>> GetByStatusAsync(PushNotificationStatus status, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<PushNotificationMessage>());
    }

    private sealed class RecoveryInstallationRepository(
        PushInstallation installation,
        Id<UserSession> replacementSessionId) : IPushInstallationRepository
    {
        public CancellationToken ResetCancellationToken { get; private set; }
        public bool ResetCompleted { get; private set; }

        public Task<PushInstallation?> FindByIdAsync(Id<PushInstallation> id, CancellationToken cancellationToken = default)
            => Task.FromResult<PushInstallation?>(id == installation.Id ? installation : null);
        public Task<PushInstallation?> FindActiveByIdAsync(Id<PushInstallation> id, CancellationToken cancellationToken = default)
            => Task.FromResult<PushInstallation?>(id == installation.Id && installation.DisabledAt == null ? installation : null);
        public Task<PushInstallationDeliveryTarget?> FindActiveBoundTargetAsync(Id<PushInstallation> id, Id<User> userId, Id<UserSession> sessionId, long registrationRevision, CancellationToken cancellationToken = default)
            => Task.FromResult<PushInstallationDeliveryTarget?>(id == installation.Id && userId == installation.UserId && sessionId == installation.SessionId && registrationRevision == installation.RegistrationRevision && installation.DisabledAt == null
                ? new PushInstallationDeliveryTarget(installation.Id, installation.UserId!.Value, installation.SessionId!.Value, installation.RegistrationRevision)
                : null);
        public Task<PushInstallation?> FindByInstallationIdAsync(string installationId, CancellationToken cancellationToken = default)
            => Task.FromResult<PushInstallation?>(null);
        public Task<PushInstallation?> FindBoundToUserAndSessionAsync(string installationId, Id<User> userId, Id<UserSession> sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult<PushInstallation?>(null);
        public Task<List<PushInstallation>> GetActiveByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<PushInstallation>());
        public Task<List<PushInstallation>> GetBySessionIdAsync(Id<UserSession> sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<PushInstallation>());
        public Task<List<PushInstallation>> GetStaleActiveAsync(DateTimeOffset lastSeenBefore, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<PushInstallation>());
        public Task AddAsync(PushInstallation item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(PushInstallation item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PushInstallation?> UpsertForUserSessionAsync(PushInstallationRegistration registration, CancellationToken cancellationToken = default)
            => Task.FromResult<PushInstallation?>(null);
        public Task<PushInstallation?> DisableBoundForUserAndSessionAsync(string installationId, Id<User> userId, Id<UserSession> sessionId, DateTimeOffset disabledAt, string disabledReason, CancellationToken cancellationToken = default)
            => Task.FromResult<PushInstallation?>(null);
        public Task<PushInstallation?> DisassociateBoundForUserAndSessionAsync(string installationId, Id<User> userId, Id<UserSession> sessionId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken = default)
            => Task.FromResult<PushInstallation?>(null);
        public Task ResetFailedMutationAsync(PushInstallation item, CancellationToken cancellationToken = default)
        {
            ResetCancellationToken = cancellationToken;
            item.FcmToken = "token-b";
            item.SessionId = replacementSessionId;
            item.RegistrationRevision = 1;
            item.DisabledAt = null;
            item.DisabledReason = null;
            ResetCompleted = true;
            return Task.CompletedTask;
        }
        public Task DisassociateForSessionAsync(Id<AccountSessionReference> sessionId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task RemoveForAccountAsync(Id<User> userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InvalidTokenSender : IPushProviderSender
    {
        public Task<PushSendAttemptResult> SendAsync(PushInstallationDeliveryTarget target, PushEventPayload payload, CancellationToken cancellationToken = default)
            => Task.FromResult(new PushSendAttemptResult(PushSendOutcome.InvalidToken, "BadRequest", null, "UNREGISTERED", "unregistered"));
    }

    private sealed class RecoveryUnitOfWork(RecoveryInstallationRepository installationRepository) : IUnitOfWork
    {
        private bool _firstSave = true;

        public CancellationToken RecoverySaveCancellationToken { get; private set; }
        public bool RecoverySaveObservedAfterReset { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_firstSave)
            {
                _firstSave = false;
                return Task.FromException<int>(new DbUpdateConcurrencyException());
            }

            RecoverySaveCancellationToken = cancellationToken;
            RecoverySaveObservedAfterReset = installationRepository.ResetCompleted;
            return Task.FromResult(1);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class AllowAllDisclosureEligibility : INotificationDisclosureEligibilityService
    {
        public Task<bool> CanDiscloseAsync(Id<InAppNotification> notificationId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class NoOpPushScheduler : IPushBackgroundScheduler
    {
        public string? Enqueue(string notificationId) => null;
        public string? ScheduleRetry(string notificationId, TimeSpan delay) => null;
    }

    private sealed class NoRetrySettings : IPushNotificationDeliveryRetrySettings
    {
        public IReadOnlyList<int> RetryDelaysSeconds { get; } = [];
    }

    private sealed class ActiveAccountResolver : IAuthenticatedAccountContextResolver
    {
        public Task<AuthenticatedAccountResolution> ResolveAsync(Id<AccountReference> accountId, Id<AccountSessionReference> sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(new AuthenticatedAccountResolution(
                AuthenticatedAccountResolutionStatus.Active,
                new AuthenticatedAccountContext(accountId, sessionId, [], [], false, false)));
    }
}
