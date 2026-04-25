using FluentAssertions;
using LgymApi.Application.Features.AdminManagement.Models;
using LgymApi.Application.Repositories;
using LgymApi.Application.Models;
using LgymApi.Application.Pagination;
using LgymApi.Application.Features.TrainerRelationships.Models;
using LgymApi.Application.Options;
using LgymApi.BackgroundWorker.Actions;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class SendInvitationEmailHandlerTests
{
    private TestTrainerRelationshipRepository _testInvitationRepository = null!;
    private TestUserRepository _testUserRepository = null!;
    private TestEmailScheduler _testScheduler = null!;
    private TestEmailNotificationsFeature _testEmailNotificationsFeature = null!;
    private TestLogger _testLogger = null!;
    private SendInvitationEmailHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _testInvitationRepository = new TestTrainerRelationshipRepository();
        _testUserRepository = new TestUserRepository();
        _testScheduler = new TestEmailScheduler();
        _testEmailNotificationsFeature = new TestEmailNotificationsFeature();
        _testLogger = new TestLogger();
        _handler = new SendInvitationEmailHandler(_testInvitationRepository, _testUserRepository, _testScheduler, _testEmailNotificationsFeature, _testLogger, new AppDefaultsOptions());
    }

        [Test]
        public async Task ExecuteAsync_WithValidCommand_SchedulesInvitationEmail()
        {
            // Arrange
            var invitationId = Id<TrainerInvitation>.New();
            var trainerId = Id<User>.New();
            var traineeId = Id<User>.New();
            var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

            _testInvitationRepository.InvitationToReturn = new TrainerInvitation
            {
                Id = (Domain.ValueObjects.Id<TrainerInvitation>)invitationId,
                Code = "ABC123XYZ",
                ExpiresAt = expiresAt,
                TrainerId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)(Domain.ValueObjects.Id<User>)trainerId,
                TraineeId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)(Domain.ValueObjects.Id<User>)traineeId
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)traineeId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)traineeId,
            Email = "trainee@example.com",
            PreferredLanguage = "en-US"
        };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)trainerId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)trainerId,
                Name = "Coach Smith",
                PreferredLanguage = "en-US"
            };

        var command = new InvitationCreatedCommand
        {
            InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)invitationId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        _testScheduler.ScheduledPayloads.Should().HaveCount(1);
        var payload = _testScheduler.ScheduledPayloads[0];
        payload.InvitationId.Should().Be(invitationId);
        payload.InvitationCode.Should().Be("ABC123XYZ");
        payload.ExpiresAt.Should().Be(expiresAt);
        payload.TrainerName.Should().Be("Coach Smith");
        payload.RecipientEmail.Should().Be("trainee@example.com");
        payload.CultureName.Should().Be("en-US");
        payload.PreferredTimeZone.Should().Be("Europe/Warsaw");
    }

    [Test]
        public async Task ExecuteAsync_WithEmptyEmail_SkipsSchedulingGracefully()
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

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)traineeId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)traineeId,
                Email = string.Empty,
                PreferredLanguage = "en-US"
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)trainerId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)trainerId,
                Name = "Coach"
            };

        var command = new InvitationCreatedCommand
        {
            InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)invitationId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        _testScheduler.ScheduledPayloads.Should().BeEmpty();
        _testLogger.WarningMessages.Should().HaveCount(1);
        _testLogger.WarningMessages[0].Should().Contain("no recipient email");
    }

    [Test]
        public async Task ExecuteAsync_WithWhitespaceEmail_SkipsSchedulingGracefully()
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

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)traineeId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)traineeId,
                Email = "   ",
                PreferredLanguage = "en-US"
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)trainerId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)trainerId,
                Name = "Coach"
            };

        var command = new InvitationCreatedCommand
        {
            InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)invitationId
        };

        // Act
        await _handler.ExecuteAsync(command);

         // Assert
         _testScheduler.ScheduledPayloads.Should().BeEmpty();
         _testLogger.WarningMessages.Should().HaveCount(1);
     }

    [Test]
        public async Task ExecuteAsync_MapsAllFieldsToPayload()
        {
            // Arrange
            var invitationId = Id<TrainerInvitation>.New();
            var trainerId = Id<User>.New();
            var traineeId = Id<User>.New();
            var expiresAt = DateTimeOffset.UtcNow.AddDays(14);

            _testInvitationRepository.InvitationToReturn = new TrainerInvitation
            {
                Id = (Domain.ValueObjects.Id<TrainerInvitation>)invitationId,
                Code = "INVITE2024",
                ExpiresAt = expiresAt,
                TrainerId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)(Domain.ValueObjects.Id<User>)trainerId,
                TraineeId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)(Domain.ValueObjects.Id<User>)traineeId
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)traineeId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)traineeId,
                Email = "carlos@example.es",
                PreferredLanguage = "es-ES",
                PreferredTimeZone = "Europe/Madrid"
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)trainerId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)trainerId,
                Name = "Maria Rodriguez",
                PreferredLanguage = "es-ES"
            };

        var command = new InvitationCreatedCommand
        {
            InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)invitationId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        var payload = _testScheduler.ScheduledPayloads[0];
        payload.InvitationId.Should().Be(invitationId);
        payload.InvitationCode.Should().Be("INVITE2024");
        payload.ExpiresAt.Should().Be(expiresAt);
        payload.TrainerName.Should().Be("Maria Rodriguez");
        payload.RecipientEmail.Should().Be("carlos@example.es");
        payload.CultureName.Should().Be("es-ES");
        payload.PreferredTimeZone.Should().Be("Europe/Madrid");
    }

    [Test]
        public async Task ExecuteAsync_WithCancellationToken_PassesTokenToScheduler()
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

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)traineeId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)traineeId,
                Email = "test@example.com",
                PreferredLanguage = "en-US"
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)trainerId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)trainerId,
                Name = "Coach"
            };

        var command = new InvitationCreatedCommand
        {
            InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)invitationId
        };

        using var cts = new CancellationTokenSource();

        // Act
        await _handler.ExecuteAsync(command, cts.Token);

        // Assert
        _testScheduler.ReceivedToken.Should().Be(cts.Token);
    }

    [Test]
        public async Task ExecuteAsync_LogsInformationOnSuccess()
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

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)traineeId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)traineeId,
                Email = "test@example.com",
                PreferredLanguage = "en-US"
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)trainerId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)trainerId,
                Name = "Coach"
            };

        var command = new InvitationCreatedCommand
        {
            InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)invitationId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        _testLogger.InformationMessages.Should().HaveCount(1);
        _testLogger.InformationMessages[0].Should().Contain("Invitation email scheduled");
    }

    [Test]
    public void Constructor_WithNullInvitationRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var action = () => new SendInvitationEmailHandler(null!, _testUserRepository, _testScheduler, _testEmailNotificationsFeature, _testLogger, new AppDefaultsOptions());
        var ex = action.Should().Throw<ArgumentNullException>().Which;
        ex.ParamName.Should().Be("invitationRepository");
    }

    [Test]
    public void Constructor_WithNullUserRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var action = () => new SendInvitationEmailHandler(_testInvitationRepository, null!, _testScheduler, _testEmailNotificationsFeature, _testLogger, new AppDefaultsOptions());
        var ex = action.Should().Throw<ArgumentNullException>().Which;
        ex.ParamName.Should().Be("userRepository");
    }

    [Test]
    public void Constructor_WithNullEmailScheduler_ThrowsArgumentNullException()
    {
        // Act & Assert
        var action = () => new SendInvitationEmailHandler(_testInvitationRepository, _testUserRepository, null!, _testEmailNotificationsFeature, _testLogger, new AppDefaultsOptions());
        var ex = action.Should().Throw<ArgumentNullException>().Which;
        ex.ParamName.Should().Be("emailScheduler");
    }

    [Test]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var action = () => new SendInvitationEmailHandler(_testInvitationRepository, _testUserRepository, _testScheduler, _testEmailNotificationsFeature, null!, new AppDefaultsOptions());
        var ex = action.Should().Throw<ArgumentNullException>().Which;
        ex.ParamName.Should().Be("logger");
    }

    [Test]
        public async Task ExecuteAsync_WithDifferentCulture_PreservesCultureName()
        {
            // Arrange
            var invitationId = Id<TrainerInvitation>.New();
            var trainerId = Id<User>.New();
            var traineeId = Id<User>.New();

            _testInvitationRepository.InvitationToReturn = new TrainerInvitation
            {
                Id = (Domain.ValueObjects.Id<TrainerInvitation>)invitationId,
                Code = "FR123",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                TrainerId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)(Domain.ValueObjects.Id<User>)trainerId,
                TraineeId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)(Domain.ValueObjects.Id<User>)traineeId
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)traineeId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)traineeId,
                Email = "trainee@example.fr",
                PreferredLanguage = "fr-FR",
                PreferredTimeZone = "Europe/Paris"
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)trainerId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)trainerId,
                Name = "Coach Pierre",
                PreferredLanguage = "fr-FR"
            };

        var command = new InvitationCreatedCommand
        {
            InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)invitationId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        var payload = _testScheduler.ScheduledPayloads[0];
        payload.CultureName.Should().Be("fr-FR");
        payload.PreferredTimeZone.Should().Be("Europe/Paris");
    }

    [Test]
        public async Task ExecuteAsync_UsesConfiguredDefaults_WhenLanguageAndTimeZoneWhitespace()
        {
            var invitationId = Id<TrainerInvitation>.New();
            var trainerId = Id<User>.New();
            var traineeId = Id<User>.New();

            _testInvitationRepository.InvitationToReturn = new TrainerInvitation
            {
                Id = (Domain.ValueObjects.Id<TrainerInvitation>)invitationId,
                Code = "CFG123",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                TrainerId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)(Domain.ValueObjects.Id<User>)trainerId,
                TraineeId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)(Domain.ValueObjects.Id<User>)traineeId
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)traineeId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)traineeId,
                Email = "trainee@example.com",
                PreferredTimeZone = "   "
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)trainerId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)trainerId,
                Name = "Coach",
                PreferredLanguage = "   "
            };

        var handler = new SendInvitationEmailHandler(
            _testInvitationRepository,
            _testUserRepository,
            _testScheduler,
            _testEmailNotificationsFeature,
            _testLogger,
            new AppDefaultsOptions { PreferredLanguage = "pl-PL", PreferredTimeZone = "UTC" });

        await handler.ExecuteAsync(new InvitationCreatedCommand { InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)invitationId });

        var payload = _testScheduler.ScheduledPayloads[0];
        payload.CultureName.Should().Be("pl-PL");
        payload.PreferredTimeZone.Should().Be("UTC");
    }

    [Test]
        public async Task ExecuteAsync_WithShortExpirationPeriod_PreservesExpiresAt()
        {
            // Arrange
            var invitationId = Id<TrainerInvitation>.New();
            var trainerId = Id<User>.New();
            var traineeId = Id<User>.New();
            var expiresAt = DateTimeOffset.UtcNow.AddHours(24);

            _testInvitationRepository.InvitationToReturn = new TrainerInvitation
            {
                Id = (Domain.ValueObjects.Id<TrainerInvitation>)invitationId,
                Code = "URGENT",
                ExpiresAt = expiresAt,
                TrainerId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)(Domain.ValueObjects.Id<User>)trainerId,
                TraineeId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)(Domain.ValueObjects.Id<User>)traineeId
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)traineeId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)traineeId,
                Email = "trainee@example.com",
                PreferredLanguage = "en-US"
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)trainerId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)trainerId,
                Name = "Coach"
            };

        var command = new InvitationCreatedCommand
        {
            InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)invitationId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        var payload = _testScheduler.ScheduledPayloads[0];
        payload.ExpiresAt.Should().Be(expiresAt);
    }

    [Test]
    public async Task ExecuteAsync_WithInvitationNotFound_SkipsSchedulingGracefully()
    {
        // Arrange
        _testInvitationRepository.InvitationToReturn = null;

        var command = new InvitationCreatedCommand
        {
            InvitationId = Id<TrainerInvitation>.New()
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        _testScheduler.ScheduledPayloads.Should().BeEmpty();
        _testLogger.WarningMessages.Should().HaveCount(1);
        _testLogger.WarningMessages[0].Should().Contain("Invitation not found");
    }

    [Test]
        public async Task ExecuteAsync_WithTraineeNotFound_SkipsSchedulingGracefully()
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

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)trainerId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)trainerId,
                Name = "Coach"
            };

         // traineeId not added to repository

         var command = new InvitationCreatedCommand
         {
             InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)invitationId
         };

         // Act
         await _handler.ExecuteAsync(command);

         // Assert
         _testScheduler.ScheduledPayloads.Should().BeEmpty();
         _testLogger.WarningMessages.Should().HaveCount(1);
         _testLogger.WarningMessages[0].Should().Contain("Trainee user not found");
    }

    [Test]
        public async Task ExecuteAsync_WithTrainerNotFound_SkipsSchedulingGracefully()
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

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)traineeId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)traineeId,
                Email = "trainee@example.com"
            };

        // trainerId not added to repository

        var command = new InvitationCreatedCommand
        {
            InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)invitationId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        _testScheduler.ScheduledPayloads.Should().BeEmpty();
        _testLogger.WarningMessages.Should().HaveCount(1);
        _testLogger.WarningMessages[0].Should().Contain("Trainer user not found");
    }

    [Test]
        public async Task ExecuteAsync_WithFeatureDisabled_SkipsEmailScheduling()
        {
            // Arrange
            _testEmailNotificationsFeature.Enabled = false;
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

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)traineeId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)traineeId,
                Email = "trainee@example.com",
                PreferredLanguage = "en-US"
            };

            _testUserRepository.UsersById[(Domain.ValueObjects.Id<User>)trainerId] =  new User
            {
                Id = (Domain.ValueObjects.Id<User>)trainerId,
                Name = "Coach"
            };

        var command = new InvitationCreatedCommand
        {
            InvitationId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.TrainerInvitation>)invitationId
        };

        // Act
        await _handler.ExecuteAsync(command);

         // Assert
         _testScheduler.ScheduledPayloads.Should().BeEmpty();
         _testLogger.InformationMessages.Should().HaveCount(0);
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

    private sealed class TestEmailScheduler : IEmailScheduler<InvitationEmailPayload>
    {
        public List<InvitationEmailPayload> ScheduledPayloads { get; } = new();
        public CancellationToken ReceivedToken { get; private set; }

        public Task ScheduleAsync(InvitationEmailPayload payload, CancellationToken cancellationToken = default)
        {
            ScheduledPayloads.Add(payload);
            ReceivedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class TestCommandDispatcher : ICommandDispatcher
    {
        public Task EnqueueAsync<TCommand>(TCommand command) where TCommand : class, IActionCommand
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestLogger : ILogger<SendInvitationEmailHandler>
    {
        public List<string> WarningMessages { get; } = new();
        public List<string> InformationMessages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            if (logLevel == LogLevel.Warning)
                WarningMessages.Add(message);
            else if (logLevel == LogLevel.Information)
                InformationMessages.Add(message);
        }
    }

    private sealed class TestEmailNotificationsFeature : IEmailNotificationsFeature
    {
        public bool Enabled { get; set; } = true;
    }
}
