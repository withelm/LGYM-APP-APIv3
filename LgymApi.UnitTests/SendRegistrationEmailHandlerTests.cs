using LgymApi.Application.Repositories;
using LgymApi.Application.Models;
using LgymApi.BackgroundWorker.Actions;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Application.Options;
using LgymApi.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class SendRegistrationEmailHandlerTests
{
    private TestUserRepository _testUserRepository = null!;
    private TestEmailScheduler _testScheduler = null!;
    private TestLogger _testLogger = null!;
    private SendRegistrationEmailHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _testUserRepository = new TestUserRepository();
        _testScheduler = new TestEmailScheduler();
        _testLogger = new TestLogger();
        _handler = new SendRegistrationEmailHandler(_testUserRepository, _testScheduler, _testLogger, new AppDefaultsOptions());
    }

    [Test]
    public async Task ExecuteAsync_WithValidCommand_SchedulesWelcomeEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _testUserRepository.UserToReturn = new User
        {
            Id = (Domain.ValueObjects.Id<User>)userId,
            Name = "JohnDoe",
            Email = "john.doe@example.com",
            PreferredLanguage = "en-US"
        };

        var command = new UserRegisteredCommand
        {
            UserId = userId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testScheduler.ScheduledPayloads, Has.Count.EqualTo(1));
        var payload = _testScheduler.ScheduledPayloads[0];
        Assert.That(payload.UserId, Is.EqualTo(userId));
        Assert.That(payload.UserName, Is.EqualTo("JohnDoe"));
        Assert.That(payload.RecipientEmail, Is.EqualTo("john.doe@example.com"));
        Assert.That(payload.CultureName, Is.EqualTo("en-US"));
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyEmail_SkipsSchedulingGracefully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _testUserRepository.UserToReturn = new User
        {
            Id = (Domain.ValueObjects.Id<User>)userId,
            Name = "TestUser",
            Email = string.Empty,
            PreferredLanguage = "en-US"
        };

        var command = new UserRegisteredCommand
        {
            UserId = userId
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
        var userId = Guid.NewGuid();
        _testUserRepository.UserToReturn = new User
        {
            Id = (Domain.ValueObjects.Id<User>)userId,
            Name = "TestUser",
            Email = "   ",
            PreferredLanguage = "en-US"
        };

        var command = new UserRegisteredCommand
        {
            UserId = userId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testScheduler.ScheduledPayloads, Is.Empty);
        Assert.That(_testLogger.WarningMessages, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ExecuteAsync_MapsAllUserFieldsToPayload()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _testUserRepository.UserToReturn = new User
        {
            Id = (Domain.ValueObjects.Id<User>)userId,
            Name = "MariaGarcia",
            Email = "maria.garcia@example.com",
            PreferredLanguage = "es-ES"
        };

        var command = new UserRegisteredCommand
        {
            UserId = userId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        var payload = _testScheduler.ScheduledPayloads[0];
        Assert.That(payload.UserId, Is.EqualTo(userId));
        Assert.That(payload.UserName, Is.EqualTo("MariaGarcia"));
        Assert.That(payload.RecipientEmail, Is.EqualTo("maria.garcia@example.com"));
        Assert.That(payload.CultureName, Is.EqualTo("es-ES"));
    }

    [Test]
    public async Task ExecuteAsync_WithCancellationToken_PassesTokenToScheduler()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _testUserRepository.UserToReturn = new User
        {
            Id = (Domain.ValueObjects.Id<User>)userId,
            Name = "TestUser",
            Email = "test@example.com",
            PreferredLanguage = "en-US"
        };

        var command = new UserRegisteredCommand
        {
            UserId = userId
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
        var userId = Guid.NewGuid();
        _testUserRepository.UserToReturn = new User
        {
            Id = (Domain.ValueObjects.Id<User>)userId,
            Name = "TestUser",
            Email = "test@example.com",
            PreferredLanguage = "en-US"
        };

        var command = new UserRegisteredCommand
        {
            UserId = userId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testLogger.InformationMessages, Has.Count.EqualTo(1));
        Assert.That(_testLogger.InformationMessages[0], Does.Contain("Welcome email scheduled"));
    }

    [Test]
    public void Constructor_WithNullUserRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new SendRegistrationEmailHandler(null!, _testScheduler, _testLogger, new AppDefaultsOptions()));
        Assert.That(ex.ParamName, Is.EqualTo("userRepository"));
    }

    [Test]
    public void Constructor_WithNullEmailScheduler_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new SendRegistrationEmailHandler(_testUserRepository, null!, _testLogger, new AppDefaultsOptions()));
        Assert.That(ex.ParamName, Is.EqualTo("emailScheduler"));
    }

    [Test]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new SendRegistrationEmailHandler(_testUserRepository, _testScheduler, null!, new AppDefaultsOptions()));
        Assert.That(ex.ParamName, Is.EqualTo("logger"));
    }

    [Test]
    public async Task ExecuteAsync_WithDifferentCulture_PreservesCultureName()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _testUserRepository.UserToReturn = new User
        {
            Id = (Domain.ValueObjects.Id<User>)userId,
            Name = "PierreDupont",
            Email = "pierre@example.fr",
            PreferredLanguage = "fr-FR"
        };

        var command = new UserRegisteredCommand
        {
            UserId = userId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        var payload = _testScheduler.ScheduledPayloads[0];
        Assert.That(payload.CultureName, Is.EqualTo("fr-FR"));
    }

    [Test]
    public async Task ExecuteAsync_WithUserNotFound_SkipsSchedulingGracefully()
    {
        // Arrange
        _testUserRepository.UserToReturn = null;

        var command = new UserRegisteredCommand
        {
            UserId = Guid.NewGuid()
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testScheduler.ScheduledPayloads, Is.Empty);
        Assert.That(_testLogger.WarningMessages, Has.Count.EqualTo(1));
        Assert.That(_testLogger.WarningMessages[0], Does.Contain("user not found"));
    }

    [Test]
    public async Task ExecuteAsync_WithNoPreferredLanguage_DefaultsToEnUs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _testUserRepository.UserToReturn = new User
        {
            Id = (Domain.ValueObjects.Id<User>)userId,
            Name = "TestUser",
            Email = "test@example.com",
            PreferredLanguage = null
        };

        var command = new UserRegisteredCommand
        {
            UserId = userId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        var payload = _testScheduler.ScheduledPayloads[0];
        Assert.That(payload.CultureName, Is.EqualTo("en-US"));
    }

    [Test]
    public async Task ExecuteAsync_WithWhitespacePreferredLanguage_UsesConfiguredDefault()
    {
        var userId = Guid.NewGuid();
        _testUserRepository.UserToReturn = new User
        {
            Id = (Domain.ValueObjects.Id<User>)userId,
            Name = "TestUser",
            Email = "test@example.com",
            PreferredLanguage = "   "
        };

        var handler = new SendRegistrationEmailHandler(
            _testUserRepository,
            _testScheduler,
            _testLogger,
            new AppDefaultsOptions { PreferredLanguage = "pl-PL", PreferredTimeZone = "Europe/Warsaw" });

        var command = new UserRegisteredCommand { UserId = userId };

        await handler.ExecuteAsync(command);

        var payload = _testScheduler.ScheduledPayloads[0];
        Assert.That(payload.CultureName, Is.EqualTo("pl-PL"));
    }

    // Test doubles
    private sealed class TestUserRepository : IUserRepository
    {
        public User? UserToReturn { get; set; }

        public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(UserToReturn);
        }

        public Task<User?> FindByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByNameOrEmailAsync(string name, string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<UserRankingEntry>> GetRankingAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TestEmailScheduler : IEmailScheduler<WelcomeEmailPayload>
    {
        public List<WelcomeEmailPayload> ScheduledPayloads { get; } = new();
        public CancellationToken ReceivedToken { get; private set; }

        public Task ScheduleAsync(WelcomeEmailPayload payload, CancellationToken cancellationToken = default)
        {
            ScheduledPayloads.Add(payload);
            ReceivedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class TestLogger : ILogger<SendRegistrationEmailHandler>
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
}
