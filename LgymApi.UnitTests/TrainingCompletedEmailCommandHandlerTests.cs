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
    private TestEmailScheduler _testScheduler = null!;
    private TestLogger _testLogger = null!;
    private TrainingCompletedEmailCommandHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _testScheduler = new TestEmailScheduler();
        _testLogger = new TestLogger();
        _handler = new TrainingCompletedEmailCommandHandler(_testScheduler, _testLogger);
    }

    [Test]
    public async Task ExecuteAsync_WithValidCommand_SchedulesEmail()
    {
        // Arrange
        var command = new TrainingCompletedCommand
        {
            UserId = Guid.NewGuid(),
            TrainingId = Guid.NewGuid(),
            RecipientEmail = "user@example.com",
            CultureName = "en-US",
            PlanDayName = "Day 1 - Chest",
            TrainingDate = DateTimeOffset.UtcNow,
            Exercises = new[]
            {
                new TrainingExerciseSummary
                {
                    ExerciseName = "Bench Press",
                    Series = 3,
                    Reps = 10,
                    Weight = 80,
                    Unit = WeightUnits.Kilograms
                }
            }
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testScheduler.ScheduledPayloads, Has.Count.EqualTo(1));
        var payload = _testScheduler.ScheduledPayloads[0];
        Assert.That(payload.UserId, Is.EqualTo(command.UserId));
        Assert.That(payload.TrainingId, Is.EqualTo(command.TrainingId));
        Assert.That(payload.RecipientEmail, Is.EqualTo(command.RecipientEmail));
        Assert.That(payload.CultureName, Is.EqualTo(command.CultureName));
        Assert.That(payload.PlanDayName, Is.EqualTo(command.PlanDayName));
        Assert.That(payload.TrainingDate, Is.EqualTo(command.TrainingDate));
        Assert.That(payload.Exercises, Has.Count.EqualTo(1));
        Assert.That(payload.Exercises[0].ExerciseName, Is.EqualTo("Bench Press"));
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyEmail_SkipsSchedulingGracefully()
    {
        // Arrange
        var command = new TrainingCompletedCommand
        {
            UserId = Guid.NewGuid(),
            TrainingId = Guid.NewGuid(),
            RecipientEmail = string.Empty,
            CultureName = "en-US",
            PlanDayName = "Day 1",
            TrainingDate = DateTimeOffset.UtcNow,
            Exercises = Array.Empty<TrainingExerciseSummary>()
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
        var command = new TrainingCompletedCommand
        {
            UserId = Guid.NewGuid(),
            TrainingId = Guid.NewGuid(),
            RecipientEmail = "   ",
            CultureName = "en-US",
            PlanDayName = "Day 1",
            TrainingDate = DateTimeOffset.UtcNow,
            Exercises = Array.Empty<TrainingExerciseSummary>()
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testScheduler.ScheduledPayloads, Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_MapsAllCommandFieldsToPayload()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var trainingDate = DateTimeOffset.UtcNow.AddDays(-1);
        var exercises = new[]
        {
            new TrainingExerciseSummary
            {
                ExerciseName = "Squat",
                Series = 4,
                Reps = 8,
                Weight = 100,
                Unit = WeightUnits.Kilograms
            },
            new TrainingExerciseSummary
            {
                ExerciseName = "Deadlift",
                Series = 3,
                Reps = 5,
                Weight = 120,
                Unit = WeightUnits.Kilograms
            }
        };

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = trainingId,
            RecipientEmail = "athlete@example.com",
            CultureName = "pl-PL",
            PlanDayName = "Dzień 2 - Nogi",
            TrainingDate = trainingDate,
            Exercises = exercises
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        var payload = _testScheduler.ScheduledPayloads[0];
        Assert.That(payload.UserId, Is.EqualTo(userId));
        Assert.That(payload.TrainingId, Is.EqualTo(trainingId));
        Assert.That(payload.RecipientEmail, Is.EqualTo("athlete@example.com"));
        Assert.That(payload.CultureName, Is.EqualTo("pl-PL"));
        Assert.That(payload.PlanDayName, Is.EqualTo("Dzień 2 - Nogi"));
        Assert.That(payload.TrainingDate, Is.EqualTo(trainingDate));
        Assert.That(payload.Exercises, Has.Count.EqualTo(2));
        Assert.That(payload.Exercises[0].ExerciseName, Is.EqualTo("Squat"));
        Assert.That(payload.Exercises[1].ExerciseName, Is.EqualTo("Deadlift"));
    }

    [Test]
    public async Task ExecuteAsync_WithCancellationToken_PassesTokenToScheduler()
    {
        // Arrange
        var command = new TrainingCompletedCommand
        {
            UserId = Guid.NewGuid(),
            TrainingId = Guid.NewGuid(),
            RecipientEmail = "user@example.com",
            CultureName = "en-US",
            PlanDayName = "Day 1",
            TrainingDate = DateTimeOffset.UtcNow,
            Exercises = Array.Empty<TrainingExerciseSummary>()
        };

        using var cts = new CancellationTokenSource();

        // Act
        await _handler.ExecuteAsync(command, cts.Token);

        // Assert
        Assert.That(_testScheduler.ReceivedToken, Is.EqualTo(cts.Token));
    }

    [Test]
    public void Constructor_WithNullEmailScheduler_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new TrainingCompletedEmailCommandHandler(null!, _testLogger));
        Assert.That(ex.ParamName, Is.EqualTo("emailScheduler"));
    }

    [Test]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new TrainingCompletedEmailCommandHandler(_testScheduler, null!));
        Assert.That(ex.ParamName, Is.EqualTo("logger"));
    }

    [Test]
    public async Task ExecuteAsync_WithMultipleExercises_PreservesAllExerciseData()
    {
        // Arrange
        var exercises = new[]
        {
            new TrainingExerciseSummary
            {
                ExerciseName = "Exercise A",
                Series = 3,
                Reps = 12,
                Weight = 50,
                Unit = WeightUnits.Kilograms
            },
            new TrainingExerciseSummary
            {
                ExerciseName = "Exercise B",
                Series = 4,
                Reps = 8,
                Weight = 75,
                Unit = WeightUnits.Pounds
            },
            new TrainingExerciseSummary
            {
                ExerciseName = "Exercise C",
                Series = 2,
                Reps = 15,
                Weight = 30,
                Unit = WeightUnits.Kilograms
            }
        };

        var command = new TrainingCompletedCommand
        {
            UserId = Guid.NewGuid(),
            TrainingId = Guid.NewGuid(),
            RecipientEmail = "user@example.com",
            CultureName = "en-US",
            PlanDayName = "Full Body",
            TrainingDate = DateTimeOffset.UtcNow,
            Exercises = exercises
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        var payload = _testScheduler.ScheduledPayloads[0];
        Assert.That(payload.Exercises, Has.Count.EqualTo(3));
        
        Assert.That(payload.Exercises[0].ExerciseName, Is.EqualTo("Exercise A"));
        Assert.That(payload.Exercises[0].Series, Is.EqualTo(3));
        Assert.That(payload.Exercises[0].Reps, Is.EqualTo(12));
        Assert.That(payload.Exercises[0].Weight, Is.EqualTo(50));
        Assert.That(payload.Exercises[0].Unit, Is.EqualTo(WeightUnits.Kilograms));

        Assert.That(payload.Exercises[1].ExerciseName, Is.EqualTo("Exercise B"));
        Assert.That(payload.Exercises[1].Unit, Is.EqualTo(WeightUnits.Pounds));

        Assert.That(payload.Exercises[2].ExerciseName, Is.EqualTo("Exercise C"));
    }

    // Test doubles
    private sealed class TestEmailScheduler : IEmailScheduler<TrainingCompletedEmailPayload>
    {
        public List<TrainingCompletedEmailPayload> ScheduledPayloads { get; } = new();
        public CancellationToken ReceivedToken { get; private set; }

        public Task ScheduleAsync(TrainingCompletedEmailPayload payload, CancellationToken cancellationToken = default)
        {
            ScheduledPayloads.Add(payload);
            ReceivedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class TestLogger : ILogger<TrainingCompletedEmailCommandHandler>
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
