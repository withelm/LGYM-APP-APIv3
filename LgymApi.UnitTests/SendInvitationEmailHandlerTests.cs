using LgymApi.Application.Repositories;
using LgymApi.Application.Models;
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
        Assert.That(_testScheduler.ScheduledPayloads, Has.Count.EqualTo(1));
        var payload = _testScheduler.ScheduledPayloads[0];
        Assert.That(payload.InvitationId, Is.EqualTo(invitationId));
        Assert.That(payload.InvitationCode, Is.EqualTo("ABC123XYZ"));
        Assert.That(payload.ExpiresAt, Is.EqualTo(expiresAt));
        Assert.That(payload.TrainerName, Is.EqualTo("Coach Smith"));
        Assert.That(payload.RecipientEmail, Is.EqualTo("trainee@example.com"));
        Assert.That(payload.CultureName, Is.EqualTo("en-US"));
        Assert.That(payload.PreferredTimeZone, Is.EqualTo("Europe/Warsaw"));
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
        Assert.That(_testScheduler.ScheduledPayloads, Is.Empty);
        Assert.That(_testLogger.WarningMessages, Has.Count.EqualTo(1));
        Assert.That(_testLogger.WarningMessages[0], Does.Contain("no recipient email"));
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
        Assert.That(_testScheduler.ScheduledPayloads, Is.Empty);
        Assert.That(_testLogger.WarningMessages, Has.Count.EqualTo(1));
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
        Assert.That(payload.InvitationId, Is.EqualTo(invitationId));
        Assert.That(payload.InvitationCode, Is.EqualTo("INVITE2024"));
        Assert.That(payload.ExpiresAt, Is.EqualTo(expiresAt));
        Assert.That(payload.TrainerName, Is.EqualTo("Maria Rodriguez"));
        Assert.That(payload.RecipientEmail, Is.EqualTo("carlos@example.es"));
        Assert.That(payload.CultureName, Is.EqualTo("es-ES"));
        Assert.That(payload.PreferredTimeZone, Is.EqualTo("Europe/Madrid"));
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
        Assert.That(_testScheduler.ReceivedToken, Is.EqualTo(cts.Token));
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
        Assert.That(_testLogger.InformationMessages, Has.Count.EqualTo(1));
        Assert.That(_testLogger.InformationMessages[0], Does.Contain("Invitation email scheduled"));
    }

    [Test]
    public void Constructor_WithNullInvitationRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new SendInvitationEmailHandler(null!, _testUserRepository, _testScheduler, _testEmailNotificationsFeature, _testLogger, new AppDefaultsOptions()));
        Assert.That(ex.ParamName, Is.EqualTo("invitationRepository"));
    }

    [Test]
    public void Constructor_WithNullUserRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new SendInvitationEmailHandler(_testInvitationRepository, null!, _testScheduler, _testEmailNotificationsFeature, _testLogger, new AppDefaultsOptions()));
        Assert.That(ex.ParamName, Is.EqualTo("userRepository"));
    }

    [Test]
    public void Constructor_WithNullEmailScheduler_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new SendInvitationEmailHandler(_testInvitationRepository, _testUserRepository, null!, _testEmailNotificationsFeature, _testLogger, new AppDefaultsOptions()));
        Assert.That(ex.ParamName, Is.EqualTo("emailScheduler"));
    }

    [Test]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new SendInvitationEmailHandler(_testInvitationRepository, _testUserRepository, _testScheduler, _testEmailNotificationsFeature, null!, new AppDefaultsOptions()));
        Assert.That(ex.ParamName, Is.EqualTo("logger"));
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
        Assert.That(payload.CultureName, Is.EqualTo("fr-FR"));
        Assert.That(payload.PreferredTimeZone, Is.EqualTo("Europe/Paris"));
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
        Assert.That(payload.CultureName, Is.EqualTo("pl-PL"));
        Assert.That(payload.PreferredTimeZone, Is.EqualTo("UTC"));
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
        Assert.That(payload.ExpiresAt, Is.EqualTo(expiresAt));
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
        Assert.That(_testScheduler.ScheduledPayloads, Is.Empty);
        Assert.That(_testLogger.WarningMessages, Has.Count.EqualTo(1));
        Assert.That(_testLogger.WarningMessages[0], Does.Contain("Invitation not found"));
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
        Assert.That(_testScheduler.ScheduledPayloads, Is.Empty);
        Assert.That(_testLogger.WarningMessages, Has.Count.EqualTo(1));
        Assert.That(_testLogger.WarningMessages[0], Does.Contain("Trainee user not found"));
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
        Assert.That(_testScheduler.ScheduledPayloads, Is.Empty);
        Assert.That(_testLogger.WarningMessages, Has.Count.EqualTo(1));
        Assert.That(_testLogger.WarningMessages[0], Does.Contain("Trainer user not found"));
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
        Assert.Multiple(() =>
        {
            Assert.That(_testScheduler.ScheduledPayloads, Is.Empty);
            Assert.That(_testLogger.InformationMessages, Has.Count.EqualTo(0));
        });
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
        public Task<List<TrainerInvitation>> GetInvitationsByTrainerIdAsync(Id<User> trainerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasActiveLinkForTraineeAsync(Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TrainerTraineeLink?> FindActiveLinkByTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TrainerTraineeLink?> FindActiveLinkByTraineeIdAsync(Id<User> traineeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TrainerDashboardTraineeListResult> GetDashboardTraineesAsync(Id<User> trainerId, TrainerDashboardTraineeQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
        public Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByNameOrEmailAsync(string name, string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<UserRankingEntry>> GetRankingAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
