using LgymApi.BackgroundWorker.Actions;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using Microsoft.Extensions.Logging;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UserRegisteredCommandHandlerTests
{
    [Test]
    public async Task ExecuteAsync_WithValidCommand_SchedulesWelcomeEmail()
    {
        var scheduler = new FakeWelcomeScheduler();
        var handler = new UserRegisteredCommandHandler(scheduler, new FakeLogger());
        var command = new UserRegisteredCommand
        {
            UserId = Guid.NewGuid(),
            UserName = "TestUser",
            RecipientEmail = "test@example.com",
            CultureName = "en-US"
        };

        await handler.ExecuteAsync(command, CancellationToken.None);

        Assert.That(scheduler.Payloads, Has.Count.EqualTo(1));
        Assert.That(scheduler.Payloads[0].UserId, Is.EqualTo(command.UserId));
        Assert.That(scheduler.Payloads[0].RecipientEmail, Is.EqualTo(command.RecipientEmail));
        Assert.That(scheduler.Payloads[0].CultureName, Is.EqualTo("en-US"));
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyEmail_SkipsScheduling()
    {
        var scheduler = new FakeWelcomeScheduler();
        var handler = new UserRegisteredCommandHandler(scheduler, new FakeLogger());
        var command = new UserRegisteredCommand
        {
            UserId = Guid.NewGuid(),
            UserName = "TestUser",
            RecipientEmail = string.Empty,
            CultureName = "en-US"
        };

        await handler.ExecuteAsync(command, CancellationToken.None);

        Assert.That(scheduler.Payloads, Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyCulture_DefaultsToEnUs()
    {
        var scheduler = new FakeWelcomeScheduler();
        var handler = new UserRegisteredCommandHandler(scheduler, new FakeLogger());
        var command = new UserRegisteredCommand
        {
            UserId = Guid.NewGuid(),
            UserName = "TestUser",
            RecipientEmail = "test@example.com",
            CultureName = string.Empty
        };

        await handler.ExecuteAsync(command, CancellationToken.None);

        Assert.That(scheduler.Payloads, Has.Count.EqualTo(1));
        Assert.That(scheduler.Payloads[0].CultureName, Is.EqualTo("en-US"));
    }

    private sealed class FakeWelcomeScheduler : IEmailScheduler<WelcomeEmailPayload>
    {
        public List<WelcomeEmailPayload> Payloads { get; } = new();

        public Task ScheduleAsync(WelcomeEmailPayload payload, CancellationToken cancellationToken = default)
        {
            Payloads.Add(payload);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLogger : ILogger<UserRegisteredCommandHandler>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}
