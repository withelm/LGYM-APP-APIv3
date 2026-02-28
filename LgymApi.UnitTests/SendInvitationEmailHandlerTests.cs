using LgymApi.BackgroundWorker.Actions;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using Microsoft.Extensions.Logging;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class SendInvitationEmailHandlerTests
{
    private TestEmailScheduler _testScheduler = null!;
    private TestLogger _testLogger = null!;
    private SendInvitationEmailHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _testScheduler = new TestEmailScheduler();
        _testLogger = new TestLogger();
        _handler = new SendInvitationEmailHandler(_testScheduler, _testLogger);
    }

    [Test]
    public async Task ExecuteAsync_WithValidCommand_SchedulesInvitationEmail()
    {
        // Arrange
        var invitationId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var command = new InvitationCreatedCommand
        {
            InvitationId = invitationId,
            InvitationCode = "ABC123XYZ",
            ExpiresAt = expiresAt,
            TrainerName = "Coach Smith",
            RecipientEmail = "trainee@example.com",
            CultureName = "en-US"
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
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyEmail_SkipsSchedulingGracefully()
    {
        // Arrange
        var command = new InvitationCreatedCommand
        {
            InvitationId = Guid.NewGuid(),
            InvitationCode = "TEST123",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            TrainerName = "Coach",
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
        var command = new InvitationCreatedCommand
        {
            InvitationId = Guid.NewGuid(),
            InvitationCode = "TEST123",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            TrainerName = "Coach",
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
        var invitationId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(14);
        var command = new InvitationCreatedCommand
        {
            InvitationId = invitationId,
            InvitationCode = "INVITE2024",
            ExpiresAt = expiresAt,
            TrainerName = "Maria Rodriguez",
            RecipientEmail = "carlos@example.es",
            CultureName = "es-ES"
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
    }

    [Test]
    public async Task ExecuteAsync_WithCancellationToken_PassesTokenToScheduler()
    {
        // Arrange
        var command = new InvitationCreatedCommand
        {
            InvitationId = Guid.NewGuid(),
            InvitationCode = "TEST123",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            TrainerName = "Coach",
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
        var command = new InvitationCreatedCommand
        {
            InvitationId = Guid.NewGuid(),
            InvitationCode = "TEST123",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            TrainerName = "Coach",
            RecipientEmail = "test@example.com",
            CultureName = "en-US"
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testLogger.InformationMessages, Has.Count.EqualTo(1));
        Assert.That(_testLogger.InformationMessages[0], Does.Contain("Invitation email scheduled"));
    }

    [Test]
    public void Constructor_WithNullEmailScheduler_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new SendInvitationEmailHandler(null!, _testLogger));
        Assert.That(ex.ParamName, Is.EqualTo("emailScheduler"));
    }

    [Test]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new SendInvitationEmailHandler(_testScheduler, null!));
        Assert.That(ex.ParamName, Is.EqualTo("logger"));
    }

    [Test]
    public async Task ExecuteAsync_WithDifferentCulture_PreservesCultureName()
    {
        // Arrange
        var command = new InvitationCreatedCommand
        {
            InvitationId = Guid.NewGuid(),
            InvitationCode = "FR123",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            TrainerName = "Coach Pierre",
            RecipientEmail = "trainee@example.fr",
            CultureName = "fr-FR"
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        var payload = _testScheduler.ScheduledPayloads[0];
        Assert.That(payload.CultureName, Is.EqualTo("fr-FR"));
    }

    [Test]
    public async Task ExecuteAsync_WithShortExpirationPeriod_PreservesExpiresAt()
    {
        // Arrange
        var expiresAt = DateTimeOffset.UtcNow.AddHours(24);
        var command = new InvitationCreatedCommand
        {
            InvitationId = Guid.NewGuid(),
            InvitationCode = "URGENT",
            ExpiresAt = expiresAt,
            TrainerName = "Coach",
            RecipientEmail = "trainee@example.com",
            CultureName = "en-US"
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        var payload = _testScheduler.ScheduledPayloads[0];
        Assert.That(payload.ExpiresAt, Is.EqualTo(expiresAt));
    }

    // Test doubles
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
}
