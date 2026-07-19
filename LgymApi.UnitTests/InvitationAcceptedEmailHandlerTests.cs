using FluentAssertions;
using LgymApi.Application.Features.AdminManagement.Models;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Models;
using LgymApi.Application.Options;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Actions;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.Application.Coaching.Contracts.BackgroundCommands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace LgymApi.UnitTests
{
    [TestFixture]
    public sealed class InvitationAcceptedEmailHandlerTests
    {
        private TestTrainerRelationshipRepository _testInvitationRepository = null!;
        private TestUserRepository _testUserRepository = null!;
        private TestEmailScheduler _testScheduler = null!;
        private TestEmailNotificationLogRepository _testNotificationLogRepository = null!;
        private TestEmailNotificationsFeature _testEmailNotificationsFeature = null!;
        private TestLogger _testLogger = null!;
        private AppDefaultsOptions _testAppDefaultsOptions = null!;
        private InvitationAcceptedEmailHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _testInvitationRepository = new TestTrainerRelationshipRepository();
            _testUserRepository = new TestUserRepository();
            _testScheduler = new TestEmailScheduler();
            _testNotificationLogRepository = new TestEmailNotificationLogRepository();
            _testEmailNotificationsFeature = new TestEmailNotificationsFeature();
            _testLogger = new TestLogger();
            _testAppDefaultsOptions = new AppDefaultsOptions();

            _handler = new InvitationAcceptedEmailHandler(
                _testInvitationRepository,
                _testUserRepository,
                _testScheduler,
                _testNotificationLogRepository,
                _testEmailNotificationsFeature,
                _testLogger,
                _testAppDefaultsOptions);
        }

        [Test]
        public async Task ExecuteAsync_AlreadySent_ReturnsEarly()
        {
            // Arrange
            var invitationId = Id<TrainerInvitation>.New();
            var trainerId = Id<User>.New();
            var traineeId = Id<User>.New();

            _testInvitationRepository.InvitationToReturn = new TrainerInvitation
            {
                Id = (Domain.ValueObjects.Id<TrainerInvitation>)invitationId,
                Code = "TEST123",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                TrainerId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)(Domain.ValueObjects.Id<User>)trainerId,
                TraineeId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)(Domain.ValueObjects.Id<User>)traineeId
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)traineeId] = new User
            {
                Id = (Domain.ValueObjects.Id<User>)traineeId,
                Email = "trainee@example.com",
                PreferredLanguage = "en-US"
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)trainerId] = new User
            {
                Id = (Domain.ValueObjects.Id<User>)trainerId,
                Name = "Coach",
                Email = "trainer@example.com"
            };

            // Mock existing notification with Sent status
            _testNotificationLogRepository.NotificationMessages.Add(new NotificationMessage
            {
                CorrelationId = invitationId.Rebind<CorrelationScope>(),
                Recipient = "trainer@example.com",
                Type = EmailNotificationTypes.TrainerInvitationAccepted,
                Status = EmailNotificationStatus.Sent,
                Attempts = 0
            });

            var command = new InvitationAcceptedCommand
            {
                InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)invitationId
            };

            // Act
            await _handler.ExecuteAsync(command, CancellationToken.None);

            // Assert
            _testScheduler.ScheduledPayloads.Should().BeEmpty();
            _testLogger.InformationMessages.Should().Contain(message => message.Contains("already processed"));
        }

        [Test]
        public async Task ExecuteAsync_DeadLettered_ReturnsEarly()
        {
            // Arrange
            var invitationId = Id<TrainerInvitation>.New();
            var trainerId = Id<User>.New();
            var traineeId = Id<User>.New();

            _testInvitationRepository.InvitationToReturn = new TrainerInvitation
            {
                Id = (Domain.ValueObjects.Id<TrainerInvitation>)invitationId,
                Code = "TEST123",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                TrainerId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)(Domain.ValueObjects.Id<User>)trainerId,
                TraineeId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)(Domain.ValueObjects.Id<User>)traineeId
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)traineeId] = new User
            {
                Id = (Domain.ValueObjects.Id<User>)traineeId,
                Email = "trainee@example.com",
                PreferredLanguage = "en-US"
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)trainerId] = new User
            {
                Id = (Domain.ValueObjects.Id<User>)trainerId,
                Name = "Coach",
                Email = "trainer@example.com"
            };

            // Mock existing notification with Failed status and max retries
            _testNotificationLogRepository.NotificationMessages.Add(new NotificationMessage
            {
                CorrelationId = invitationId.Rebind<CorrelationScope>(),
                Recipient = "trainer@example.com",
                Type = EmailNotificationTypes.TrainerInvitationAccepted,
                Status = EmailNotificationStatus.Failed,
                Attempts = 5
            });

            var command = new InvitationAcceptedCommand
            {
                InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)invitationId
            };

            // Act
            await _handler.ExecuteAsync(command, CancellationToken.None);

            // Assert
            _testScheduler.ScheduledPayloads.Should().BeEmpty();
            _testLogger.InformationMessages.Should().Contain(message => message.Contains("max retries reached"));
        }

        [Test]
        public async Task ExecuteAsync_FailedWithRetriesRemaining_SchedulesEmail()
        {
            // Arrange
            var invitationId = Id<TrainerInvitation>.New();
            var trainerId = Id<User>.New();
            var traineeId = Id<User>.New();

            _testInvitationRepository.InvitationToReturn = new TrainerInvitation
            {
                Id = (Domain.ValueObjects.Id<TrainerInvitation>)invitationId,
                Code = "TEST123",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                TrainerId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)(Domain.ValueObjects.Id<User>)trainerId,
                TraineeId = (Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)(Domain.ValueObjects.Id<User>)traineeId
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)traineeId] = new User
            {
                Id = (Domain.ValueObjects.Id<User>)traineeId,
                Email = "trainee@example.com",
                PreferredLanguage = "en-US"
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)trainerId] = new User
            {
                Id = (Domain.ValueObjects.Id<User>)trainerId,
                Name = "Coach",
                Email = "trainer@example.com"
            };

            // Mock existing notification with Failed status but retries remaining
            _testNotificationLogRepository.NotificationMessages.Add(new NotificationMessage
            {
                CorrelationId = invitationId.Rebind<CorrelationScope>(),
                Recipient = "trainer@example.com",
                Type = EmailNotificationTypes.TrainerInvitationAccepted,
                Status = EmailNotificationStatus.Failed,
                Attempts = 3
            });

            var command = new InvitationAcceptedCommand
            {
                InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)invitationId
            };

            // Act
            await _handler.ExecuteAsync(command, CancellationToken.None);

            // Assert
            _testScheduler.ScheduledPayloads.Should().HaveCount(1);
            var payload = _testScheduler.ScheduledPayloads[0];
            payload.RecipientEmail.Should().Be("trainer@example.com");
        }

        [Test]
        public async Task ExecuteAsync_WithNoExistingNotification_ProceedsToSchedule()
        {
            // Arrange
            var invitationId = Id<TrainerInvitation>.New();
            var trainerId = Id<User>.New();
            var traineeId = Id<User>.New();

            _testInvitationRepository.InvitationToReturn = new TrainerInvitation
            {
                Id = (Domain.ValueObjects.Id<TrainerInvitation>)invitationId,
                Code = "TEST123",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                TrainerId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)(Domain.ValueObjects.Id<User>)trainerId,
                TraineeId = (Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)(Domain.ValueObjects.Id<User>)traineeId
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)traineeId] = new User
            {
                Id = (Domain.ValueObjects.Id<User>)traineeId,
                Email = "trainee@example.com",
                PreferredLanguage = "en-US"
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)trainerId] = new User
            {
                Id = (Domain.ValueObjects.Id<User>)trainerId,
                Name = "Coach",
                Email = "trainer@example.com"
            };

            // No existing notification

            var command = new InvitationAcceptedCommand
            {
                InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)invitationId
            };

            // Act
            await _handler.ExecuteAsync(command, CancellationToken.None);

            // Assert
            _testScheduler.ScheduledPayloads.Should().HaveCount(1);
            var payload = _testScheduler.ScheduledPayloads[0];
            payload.RecipientEmail.Should().Be("trainer@example.com");
        }

        // Test doubles
        private sealed class TestTrainerRelationshipRepository : ITrainerRelationshipRepository
        {
            public TrainerInvitation? InvitationToReturn { get; set; }

            public Task<TrainerInvitation?> FindInvitationByIdAsync(Id<TrainerInvitation> invitationId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(InvitationToReturn);
            }

            public Task AddInvitationAsync(TrainerInvitation invitation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<TrainerInvitation?> FindPendingInvitationAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<TrainerInvitation?> FindPendingInvitationByEmailAsync(Id<User> trainerId, string inviteeEmail, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<bool> IsEmailAlreadyTraineeAsync(Id<User> trainerId, string inviteeEmail, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<TrainerInvitation?> FindInvitationByIdWithCodeAsync(Id<TrainerInvitation> invitationId, string code, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<List<TrainerInvitation>> GetInvitationsByTrainerIdAsync(Id<User> trainerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<bool> HasActiveLinkForTraineeAsync(Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<TrainerTraineeLink?> FindActiveLinkByTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<TrainerTraineeLink?> FindActiveLinkByTraineeIdAsync(Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<TrainerDashboardTraineeListResult> GetDashboardTraineesAsync(Id<User> trainerId, TrainerDashboardTraineeQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<Pagination<TrainerInvitationResult>> GetInvitationsPaginatedAsync(Id<User> trainerId, FilterInput filterInput, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task AddLinkAsync(TrainerTraineeLink link, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task RemoveLinkAsync(TrainerTraineeLink link, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        private sealed class TestUserRepository : IUserRepository
        {
            public Dictionary<Id<User>, User> UsersById { get; } = new();

            public Task<User?> FindByIdAsync(Id<LgymApi.Domain.Entities.User> id, CancellationToken cancellationToken = default)
            {
                UsersById.TryGetValue(id, out var user);
                return Task.FromResult(user);
            }

            public Task<User?> FindByIdIncludingDeletedAsync(Id<LgymApi.Domain.Entities.User> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<User?> FindByIdWithRolesAsync(Id<LgymApi.Domain.Entities.User> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<User?> FindByNameOrEmailAsync(string name, string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<List<UserRankingEntry>> GetRankingAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<Pagination<UserResult>> GetUsersPaginatedAsync(FilterInput filterInput, bool includeDeleted, CancellationToken cancellationToken = default) => Task.FromResult(new Pagination<UserResult>());
        }

        private sealed class TestEmailScheduler : IEmailScheduler<InvitationAcceptedEmailPayload>
        {
            public List<InvitationAcceptedEmailPayload> ScheduledPayloads { get; } = new();
            public CancellationToken ReceivedToken { get; private set; }

            public Task ScheduleAsync(InvitationAcceptedEmailPayload payload, CancellationToken cancellationToken = default)
            {
                ScheduledPayloads.Add(payload);
                ReceivedToken = cancellationToken;
                return Task.CompletedTask;
            }
        }

        private sealed class TestEmailNotificationLogRepository : IEmailNotificationLogRepository
        {
            public List<NotificationMessage> NotificationMessages { get; } = new();

            public Task AddAsync(NotificationMessage message, CancellationToken cancellationToken = default)
            {
                NotificationMessages.Add(message);
                return Task.CompletedTask;
            }

            public Task<NotificationMessage?> FindByIdAsync(Id<NotificationMessage> id, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public Task<NotificationMessage?> FindByCorrelationAsync(EmailNotificationType type, Id<CorrelationScope> correlationId, string recipient, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(
                    NotificationMessages.FirstOrDefault(nm =>
                        nm.Type == type &&
                        nm.CorrelationId == correlationId &&
                        nm.Recipient == recipient));
            }

            public Task<List<NotificationMessage>> GetPendingUndispatchedAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<List<NotificationMessage>> GetFailedAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<List<NotificationMessage>> GetDeadLetteredAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<int> CountByStatusAsync(EmailNotificationStatus status, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<int> DeleteSentOlderThanAsync(DateTimeOffset cutoffDate, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<bool> TryTransitionToSendingAsync(Id<NotificationMessage> id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<List<NotificationMessage>> GetStuckSendingAsync(int emailSendLeaseSeconds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private sealed class TestEmailNotificationsFeature : IEmailNotificationsFeature
        {
            public bool Enabled { get; set; } = true;
        }

        private sealed class TestLogger : ILogger<InvitationAcceptedEmailHandler>
        {
            public List<string> InformationMessages { get; } = new();
            public List<string> WarningMessages { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var message = formatter(state, exception);
                if (logLevel == LogLevel.Information)
                    InformationMessages.Add(message);
                else if (logLevel == LogLevel.Warning)
                    WarningMessages.Add(message);
            }
        }
    }
}
