using LgymApi.BackgroundWorker.Actions;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using Microsoft.Extensions.Logging;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class SendRegistrationEmailHandlerTests
{
    private TestEmailScheduler _testScheduler = null!;
    private TestLogger _testLogger = null!;
    private SendRegistrationEmailHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _testScheduler = new TestEmailScheduler();
        _testLogger = new TestLogger();
        _handler = new SendRegistrationEmailHandler(_testScheduler, _testLogger);
    }

    [Test]
    public async Task ExecuteAsync_WithValidCommand_SchedulesWelcomeEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UserRegisteredCommand
        {
            UserId = userId,
            UserName = "JohnDoe",
            RecipientEmail = "john.doe@example.com",
            CultureName = "en-US"
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
        var command = new UserRegisteredCommand
        {
            UserId = Guid.NewGuid(),
            UserName = "TestUser",
            RecipientEmail = string.Empty,
            CultureName = "en-US"
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
        var command = new UserRegisteredCommand
        {
            UserId = Guid.NewGuid(),
            UserName = "TestUser",
            RecipientEmail = "   ",
            CultureName = "en-US"
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testScheduler.ScheduledPayloads, Is.Empty);
        Assert.That(_testLogger.WarningMessages, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ExecuteAsync_MapsAllCommandFieldsToPayload()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UserRegisteredCommand
        {
            UserId = userId,
            UserName = "MariaGarcia",
            RecipientEmail = "maria.garcia@example.com",
            CultureName = "es-ES"
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
        var command = new UserRegisteredCommand
        {
            UserId = Guid.NewGuid(),
            UserName = "TestUser",
            RecipientEmail = "test@example.com",
            CultureName = "en-US"
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
        var command = new UserRegisteredCommand
        {
            UserId = Guid.NewGuid(),
            UserName = "TestUser",
            RecipientEmail = "test@example.com",
            CultureName = "en-US"
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testLogger.InformationMessages, Has.Count.EqualTo(1));
        Assert.That(_testLogger.InformationMessages[0], Does.Contain("Welcome email scheduled"));
    }

    [Test]
    public void Constructor_WithNullEmailScheduler_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new SendRegistrationEmailHandler(null!, _testLogger));
        Assert.That(ex.ParamName, Is.EqualTo("emailScheduler"));
    }

    [Test]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new SendRegistrationEmailHandler(_testScheduler, null!));
        Assert.That(ex.ParamName, Is.EqualTo("logger"));
    }

    [Test]
    public async Task ExecuteAsync_WithDifferentCulture_PreservesCultureName()
    {
        // Arrange
        var command = new UserRegisteredCommand
        {
            UserId = Guid.NewGuid(),
            UserName = "PierreDupont",
            RecipientEmail = "pierre@example.fr",
            CultureName = "fr-FR"
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        var payload = _testScheduler.ScheduledPayloads[0];
        Assert.That(payload.CultureName, Is.EqualTo("fr-FR"));
    }

    // Test doubles
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
