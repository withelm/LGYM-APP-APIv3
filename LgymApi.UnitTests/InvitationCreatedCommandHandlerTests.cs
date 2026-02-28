using LgymApi.BackgroundWorker.Actions;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using Microsoft.Extensions.Logging;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class InvitationCreatedCommandHandlerTests
{
    [Test]
    public async Task ExecuteAsync_WithValidCommand_SchedulesInvitationEmail()
    {
        var scheduler = new FakeInvitationScheduler();
        var handler = new InvitationCreatedCommandHandler(scheduler, new FakeLogger());
        var command = new InvitationCreatedCommand
        {
            InvitationId = Guid.NewGuid(),
            InvitationCode = "TEST123",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            TrainerName = "Trainer Smith",
            RecipientEmail = "trainee@example.com",
            CultureName = "en-US"
        };

        await handler.ExecuteAsync(command, CancellationToken.None);

        Assert.That(scheduler.Payloads, Has.Count.EqualTo(1));
        Assert.That(scheduler.Payloads[0].InvitationId, Is.EqualTo(command.InvitationId));
        Assert.That(scheduler.Payloads[0].RecipientEmail, Is.EqualTo(command.RecipientEmail));
        Assert.That(scheduler.Payloads[0].CultureName, Is.EqualTo("en-US"));
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyEmail_SkipsScheduling()
    {
        var scheduler = new FakeInvitationScheduler();
        var handler = new InvitationCreatedCommandHandler(scheduler, new FakeLogger());
        var command = new InvitationCreatedCommand
        {
            InvitationId = Guid.NewGuid(),
            InvitationCode = "TEST123",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            TrainerName = "Trainer Smith",
            RecipientEmail = string.Empty,
            CultureName = "en-US"
        };

        await handler.ExecuteAsync(command, CancellationToken.None);

        Assert.That(scheduler.Payloads, Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyCulture_DefaultsToEnUs()
    {
        var scheduler = new FakeInvitationScheduler();
        var handler = new InvitationCreatedCommandHandler(scheduler, new FakeLogger());
        var command = new InvitationCreatedCommand
        {
            InvitationId = Guid.NewGuid(),
            InvitationCode = "TEST123",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            TrainerName = "Trainer Smith",
            RecipientEmail = "trainee@example.com",
            CultureName = string.Empty
        };

        await handler.ExecuteAsync(command, CancellationToken.None);

        Assert.That(scheduler.Payloads, Has.Count.EqualTo(1));
        Assert.That(scheduler.Payloads[0].CultureName, Is.EqualTo("en-US"));
    }

    private sealed class FakeInvitationScheduler : IEmailScheduler<InvitationEmailPayload>
    {
        public List<InvitationEmailPayload> Payloads { get; } = new();

        public Task ScheduleAsync(InvitationEmailPayload payload, CancellationToken cancellationToken = default)
        {
            Payloads.Add(payload);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLogger : ILogger<InvitationCreatedCommandHandler>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}
