using LgymApi.BackgroundWorker.Actions;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainingCompletedEmailCommandHandlerTests
{
    [Test]
    public async Task ExecuteAsync_WithValidCommand_SchedulesTrainingCompletedEmail()
    {
        var scheduler = new FakeTrainingCompletedScheduler();
        var handler = new TrainingCompletedEmailCommandHandler(scheduler, new FakeLogger());
        var userId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var createdAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = trainingId,
            CreatedAtUtc = createdAtUtc,
            RecipientEmail = "trainee@example.com",
            CultureName = "pl-PL",
            PlanDayName = "Push Day",
            ExerciseDetails = new[]
            {
                new TrainingExerciseDetail
                {
                    ExerciseName = "Bench Press",
                    Series = 1,
                    Reps = 8,
                    Weight = 100,
                    Unit = WeightUnits.Kilograms
                },
                new TrainingExerciseDetail
                {
                    ExerciseName = "Overhead Press",
                    Series = 2,
                    Reps = 6,
                    Weight = 60,
                    Unit = WeightUnits.Kilograms
                }
            }
        };

        await handler.ExecuteAsync(command, CancellationToken.None);

        Assert.That(scheduler.Payloads, Has.Count.EqualTo(1));
        var payload = scheduler.Payloads[0];
        Assert.That(payload.UserId, Is.EqualTo(userId));
        Assert.That(payload.TrainingId, Is.EqualTo(trainingId));
        Assert.That(payload.RecipientEmail, Is.EqualTo("trainee@example.com"));
        Assert.That(payload.CultureName, Is.EqualTo("pl-PL"));
        Assert.That(payload.PlanDayName, Is.EqualTo("Push Day"));
        Assert.That(payload.TrainingDate, Is.EqualTo(new DateTimeOffset(createdAtUtc)));
        Assert.That(payload.Exercises, Has.Count.EqualTo(2));
        Assert.That(payload.Exercises[0].ExerciseName, Is.EqualTo("Bench Press"));
        Assert.That(payload.Exercises[0].Series, Is.EqualTo(1));
        Assert.That(payload.Exercises[0].Reps, Is.EqualTo(8));
        Assert.That(payload.Exercises[0].Weight, Is.EqualTo(100));
        Assert.That(payload.Exercises[0].Unit, Is.EqualTo(WeightUnits.Kilograms));
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyEmail_SkipsScheduling()
    {
        var scheduler = new FakeTrainingCompletedScheduler();
        var handler = new TrainingCompletedEmailCommandHandler(scheduler, new FakeLogger());
        var command = new TrainingCompletedCommand
        {
            UserId = Guid.NewGuid(),
            TrainingId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            RecipientEmail = string.Empty,
            CultureName = "en-US"
        };

        await handler.ExecuteAsync(command, CancellationToken.None);

        Assert.That(scheduler.Payloads, Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyCulture_DefaultsToEnUs()
    {
        var scheduler = new FakeTrainingCompletedScheduler();
        var handler = new TrainingCompletedEmailCommandHandler(scheduler, new FakeLogger());
        var command = new TrainingCompletedCommand
        {
            UserId = Guid.NewGuid(),
            TrainingId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            RecipientEmail = "trainee@example.com",
            CultureName = string.Empty,
            PlanDayName = null,
            ExerciseDetails = Array.Empty<TrainingExerciseDetail>()
        };

        await handler.ExecuteAsync(command, CancellationToken.None);

        Assert.That(scheduler.Payloads, Has.Count.EqualTo(1));
        Assert.That(scheduler.Payloads[0].CultureName, Is.EqualTo("en-US"));
        Assert.That(scheduler.Payloads[0].PlanDayName, Is.EqualTo(string.Empty));
    }

    private sealed class FakeTrainingCompletedScheduler : IEmailScheduler<TrainingCompletedEmailPayload>
    {
        public List<TrainingCompletedEmailPayload> Payloads { get; } = new();

        public Task ScheduleAsync(TrainingCompletedEmailPayload payload, CancellationToken cancellationToken = default)
        {
            Payloads.Add(payload);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLogger : ILogger<TrainingCompletedEmailCommandHandler>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}
