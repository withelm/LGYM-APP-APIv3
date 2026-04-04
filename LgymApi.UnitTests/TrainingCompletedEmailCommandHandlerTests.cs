using LgymApi.Application.Repositories;
using LgymApi.Application.Models;
using LgymApi.Application.Options;
using LgymApi.BackgroundWorker.Actions;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainingCompletedEmailCommandHandlerTests
{
    private TestUserRepository _testUserRepository = null!;
    private TestTrainingRepository _testTrainingRepository = null!;
    private TestTrainingExerciseScoreRepository _testTrainingExerciseScoreRepository = null!;
    private TestExerciseScoreRepository _testExerciseScoreRepository = null!;
    private TestEmailNotificationSubscriptionRepository _testSubscriptionRepository = null!;
    private TestEmailScheduler _testScheduler = null!;
    private TestLogger _testLogger = null!;
    private TrainingCompletedEmailCommandHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _testUserRepository = new TestUserRepository();
        _testTrainingRepository = new TestTrainingRepository();
        _testTrainingExerciseScoreRepository = new TestTrainingExerciseScoreRepository();
        _testExerciseScoreRepository = new TestExerciseScoreRepository();
        _testSubscriptionRepository = new TestEmailNotificationSubscriptionRepository();
        _testScheduler = new TestEmailScheduler();
        _testLogger = new TestLogger();
        _handler = new TrainingCompletedEmailCommandHandler(
            _testUserRepository,
            _testTrainingRepository,
            _testTrainingExerciseScoreRepository,
            _testExerciseScoreRepository,
            _testSubscriptionRepository,
            _testScheduler,
            _testLogger,
            new AppDefaultsOptions());
    }

    [Test]
    public async Task ExecuteAsync_WithValidCommand_SchedulesEmail()
    {
        // Arrange
         var userId = Id<User>.New();
         var trainingId = Id<Training>.New();
         var exerciseScoreId = Id<ExerciseScore>.New();
         var exerciseId = Id<Exercise>.New();

        _testUserRepository.UserToReturn = new User
        {
            Id = (LgymApi.Domain.ValueObjects.Id<User>)userId,
            Email = "user@example.com",
            PreferredLanguage = "en-US"
        };

        _testTrainingRepository.TrainingToReturn = new Training
        {
            Id = (LgymApi.Domain.ValueObjects.Id<Training>)trainingId,
            PlanDay = new PlanDay { Name = "Day 1 - Chest" },
            CreatedAt = DateTimeOffset.UtcNow
        };

        _testTrainingExerciseScoreRepository.TrainingExercisesToReturn = new List<TrainingExerciseScore>
        {
            new TrainingExerciseScore
            {
                TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)(LgymApi.Domain.ValueObjects.Id<Training>)trainingId,
                ExerciseScoreId = (LgymApi.Domain.ValueObjects.Id<ExerciseScore>)exerciseScoreId
            }
        };

        _testExerciseScoreRepository.ExerciseScoresToReturn = new List<ExerciseScore>
        {
            new ExerciseScore
            {
                Id = (LgymApi.Domain.ValueObjects.Id<ExerciseScore>)exerciseScoreId,
                ExerciseId = (LgymApi.Domain.ValueObjects.Id<Exercise>)exerciseId,
                Exercise = new Exercise { Name = "Bench Press" },
                Series = 3,
                Reps = 10,
                Weight = 80,
                Unit = WeightUnits.Kilograms
            }
        };

        var command = new TrainingCompletedCommand
        {
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)userId,
            TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)trainingId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testScheduler.ScheduledPayloads, Has.Count.EqualTo(1));
        var payload = _testScheduler.ScheduledPayloads[0];
        Assert.That(payload.UserId, Is.EqualTo(userId));
        Assert.That(payload.TrainingId, Is.EqualTo(trainingId));
        Assert.That(payload.RecipientEmail, Is.EqualTo("user@example.com"));
        Assert.That(payload.CultureName, Is.EqualTo("en-US"));
        Assert.That(payload.PreferredTimeZone, Is.EqualTo("Europe/Warsaw"));
        Assert.That(payload.PlanDayName, Is.EqualTo("Day 1 - Chest"));
        Assert.That(payload.Exercises, Has.Count.EqualTo(1));
        Assert.That(payload.Exercises[0].ExerciseName, Is.EqualTo("Bench Press"));
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyEmail_SkipsSchedulingGracefully()
    {
        // Arrange
         var userId = Id<User>.New();
         var trainingId = Id<Training>.New();

        _testUserRepository.UserToReturn = new User
        {
            Id = (LgymApi.Domain.ValueObjects.Id<User>)userId,
            Email = string.Empty,
            PreferredLanguage = "en-US"
        };

        var command = new TrainingCompletedCommand
        {
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)userId,
            TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)trainingId
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
         var userId = Id<User>.New();
         var trainingId = Id<Training>.New();

        _testUserRepository.UserToReturn = new User
        {
            Id = (LgymApi.Domain.ValueObjects.Id<User>)userId,
            Email = "   ",
            PreferredLanguage = "en-US"
        };

        var command = new TrainingCompletedCommand
        {
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)userId,
            TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)trainingId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testScheduler.ScheduledPayloads, Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_MapsAllDataToPayload()
    {
        // Arrange
          var userId = Id<User>.New();
          var trainingId = Id<Training>.New();
         var exerciseScore1Id = Id<ExerciseScore>.New();
         var exerciseScore2Id = Id<ExerciseScore>.New();
         var trainingDate = DateTimeOffset.UtcNow.AddDays(-1);

        _testUserRepository.UserToReturn = new User
        {
            Id = (LgymApi.Domain.ValueObjects.Id<User>)userId,
            Email = "athlete@example.com",
            PreferredLanguage = "pl-PL"
        };

        _testTrainingRepository.TrainingToReturn = new Training
        {
            Id = (LgymApi.Domain.ValueObjects.Id<Training>)trainingId,
            PlanDay = new PlanDay { Name = "Dzień 2 - Nogi" },
            CreatedAt = trainingDate
        };

        _testTrainingExerciseScoreRepository.TrainingExercisesToReturn = new List<TrainingExerciseScore>
        {
            new TrainingExerciseScore { TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)(LgymApi.Domain.ValueObjects.Id<Training>)trainingId, ExerciseScoreId = (LgymApi.Domain.ValueObjects.Id<ExerciseScore>)exerciseScore1Id },
            new TrainingExerciseScore { TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)(LgymApi.Domain.ValueObjects.Id<Training>)trainingId, ExerciseScoreId = (LgymApi.Domain.ValueObjects.Id<ExerciseScore>)exerciseScore2Id }
        };

        _testExerciseScoreRepository.ExerciseScoresToReturn = new List<ExerciseScore>
        {
            new ExerciseScore
            {
                Id = (LgymApi.Domain.ValueObjects.Id<ExerciseScore>)exerciseScore1Id,
                Exercise = new Exercise { Name = "Squat" },
                Series = 4,
                Reps = 8,
                Weight = 100,
                Unit = WeightUnits.Kilograms
            },
            new ExerciseScore
            {
                Id = (LgymApi.Domain.ValueObjects.Id<ExerciseScore>)exerciseScore2Id,
                Exercise = new Exercise { Name = "Deadlift" },
                Series = 3,
                Reps = 5,
                Weight = 120,
                Unit = WeightUnits.Kilograms
            }
        };

        var command = new TrainingCompletedCommand
        {
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)userId,
            TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)trainingId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        var payload = _testScheduler.ScheduledPayloads[0];
        Assert.That(payload.UserId, Is.EqualTo(userId));
        Assert.That(payload.TrainingId, Is.EqualTo(trainingId));
        Assert.That(payload.RecipientEmail, Is.EqualTo("athlete@example.com"));
        Assert.That(payload.CultureName, Is.EqualTo("pl-PL"));
        Assert.That(payload.PreferredTimeZone, Is.EqualTo("Europe/Warsaw"));
        Assert.That(payload.PlanDayName, Is.EqualTo("Dzień 2 - Nogi"));
        Assert.That(payload.TrainingDate, Is.EqualTo(trainingDate));
        Assert.That(payload.Exercises, Has.Count.EqualTo(2));
        Assert.That(payload.Exercises[0].ExerciseName, Is.EqualTo("Squat"));
        Assert.That(payload.Exercises[1].ExerciseName, Is.EqualTo("Deadlift"));
    }

    [Test]
    public async Task ExecuteAsync_UsesConfiguredDefaults_WhenLanguageAndTimeZoneWhitespace()
    {
         var userId = Id<User>.New();
         var trainingId = Id<Training>.New();

        _testUserRepository.UserToReturn = new User
        {
            Id = (LgymApi.Domain.ValueObjects.Id<User>)userId,
            Email = "athlete@example.com",
            PreferredLanguage = "   ",
            PreferredTimeZone = "   "
        };

        _testTrainingRepository.TrainingToReturn = new Training
        {
            Id = (LgymApi.Domain.ValueObjects.Id<Training>)trainingId,
            PlanDay = new PlanDay { Name = "Default Day" },
            CreatedAt = DateTimeOffset.UtcNow
        };

        _testTrainingExerciseScoreRepository.TrainingExercisesToReturn = new List<TrainingExerciseScore>();
        _testExerciseScoreRepository.ExerciseScoresToReturn = new List<ExerciseScore>();

        var handler = new TrainingCompletedEmailCommandHandler(
            _testUserRepository,
            _testTrainingRepository,
            _testTrainingExerciseScoreRepository,
            _testExerciseScoreRepository,
            _testSubscriptionRepository,
            _testScheduler,
            _testLogger,
            new AppDefaultsOptions { PreferredLanguage = "pl-PL", PreferredTimeZone = "UTC" });

        await handler.ExecuteAsync(new TrainingCompletedCommand { UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)userId, TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)trainingId });

        var payload = _testScheduler.ScheduledPayloads[0];
        Assert.That(payload.CultureName, Is.EqualTo("pl-PL"));
        Assert.That(payload.PreferredTimeZone, Is.EqualTo("UTC"));
    }

    [Test]
    public async Task ExecuteAsync_WithCancellationToken_PassesTokenToScheduler()
    {
        // Arrange
         var userId = Id<User>.New();
         var trainingId = Id<Training>.New();

        _testUserRepository.UserToReturn = new User
        {
            Id = (LgymApi.Domain.ValueObjects.Id<User>)userId,
            Email = "user@example.com",
            PreferredLanguage = "en-US"
        };

        _testTrainingRepository.TrainingToReturn = new Training
        {
            Id = (LgymApi.Domain.ValueObjects.Id<Training>)trainingId,
            PlanDay = new PlanDay { Name = "Day 1" },
            CreatedAt = DateTimeOffset.UtcNow
        };

        _testTrainingExerciseScoreRepository.TrainingExercisesToReturn = new List<TrainingExerciseScore>();

        var command = new TrainingCompletedCommand
        {
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)userId,
            TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)trainingId
        };

        using var cts = new CancellationTokenSource();

        // Act
        await _handler.ExecuteAsync(command, cts.Token);

        // Assert
        Assert.That(_testScheduler.ReceivedToken, Is.EqualTo(cts.Token));
    }

    [Test]
    public void Constructor_WithNullUserRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new TrainingCompletedEmailCommandHandler(
                null!,
                _testTrainingRepository,
                _testTrainingExerciseScoreRepository,
                _testExerciseScoreRepository,
                _testSubscriptionRepository,
                _testScheduler,
                _testLogger,
                new AppDefaultsOptions()));
        Assert.That(ex.ParamName, Is.EqualTo("userRepository"));
    }

    [Test]
    public void Constructor_WithNullTrainingRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new TrainingCompletedEmailCommandHandler(
                _testUserRepository,
                null!,
                _testTrainingExerciseScoreRepository,
                _testExerciseScoreRepository,
                _testSubscriptionRepository,
                _testScheduler,
                _testLogger,
                new AppDefaultsOptions()));
        Assert.That(ex.ParamName, Is.EqualTo("trainingRepository"));
    }

    [Test]
    public void Constructor_WithNullEmailScheduler_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new TrainingCompletedEmailCommandHandler(
                _testUserRepository,
                _testTrainingRepository,
                _testTrainingExerciseScoreRepository,
                _testExerciseScoreRepository,
                _testSubscriptionRepository,
                null!,
                _testLogger,
                new AppDefaultsOptions()));
        Assert.That(ex.ParamName, Is.EqualTo("emailScheduler"));
    }

    [Test]
    public void Constructor_WithNullSubscriptionRepository_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new TrainingCompletedEmailCommandHandler(
                _testUserRepository,
                _testTrainingRepository,
                _testTrainingExerciseScoreRepository,
                _testExerciseScoreRepository,
                null!,
                _testScheduler,
                _testLogger,
                new AppDefaultsOptions()));
        Assert.That(ex.ParamName, Is.EqualTo("emailNotificationSubscriptionRepository"));
    }

    [Test]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new TrainingCompletedEmailCommandHandler(
                _testUserRepository,
                _testTrainingRepository,
                _testTrainingExerciseScoreRepository,
                _testExerciseScoreRepository,
                _testSubscriptionRepository,
                _testScheduler,
                null!,
                new AppDefaultsOptions()));
        Assert.That(ex.ParamName, Is.EqualTo("logger"));
    }

    [Test]
    public async Task ExecuteAsync_WithMultipleExercises_PreservesAllExerciseData()
    {
        // Arrange
         var userId = Id<User>.New();
         var trainingId = Id<Training>.New();
        var score1Id = Id<ExerciseScore>.New();
        var score2Id = Id<ExerciseScore>.New();
        var score3Id = Id<ExerciseScore>.New();

        _testUserRepository.UserToReturn = new User
        {
            Id = (LgymApi.Domain.ValueObjects.Id<User>)userId,
            Email = "user@example.com",
            PreferredLanguage = "en-US"
        };

        _testTrainingRepository.TrainingToReturn = new Training
        {
            Id = (LgymApi.Domain.ValueObjects.Id<Training>)trainingId,
            PlanDay = new PlanDay { Name = "Full Body" },
            CreatedAt = DateTimeOffset.UtcNow
        };

        _testTrainingExerciseScoreRepository.TrainingExercisesToReturn = new List<TrainingExerciseScore>
        {
            new TrainingExerciseScore { TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)(LgymApi.Domain.ValueObjects.Id<Training>)trainingId, ExerciseScoreId = (LgymApi.Domain.ValueObjects.Id<ExerciseScore>)score1Id },
            new TrainingExerciseScore { TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)(LgymApi.Domain.ValueObjects.Id<Training>)trainingId, ExerciseScoreId = (LgymApi.Domain.ValueObjects.Id<ExerciseScore>)score2Id },
            new TrainingExerciseScore { TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)(LgymApi.Domain.ValueObjects.Id<Training>)trainingId, ExerciseScoreId = (LgymApi.Domain.ValueObjects.Id<ExerciseScore>)score3Id }
        };

        _testExerciseScoreRepository.ExerciseScoresToReturn = new List<ExerciseScore>
        {
            new ExerciseScore
            {
                Id = (LgymApi.Domain.ValueObjects.Id<ExerciseScore>)score1Id,
                Exercise = new Exercise { Name = "Exercise A" },
                Series = 3,
                Reps = 12,
                Weight = 50,
                Unit = WeightUnits.Kilograms
            },
            new ExerciseScore
            {
                Id = (LgymApi.Domain.ValueObjects.Id<ExerciseScore>)score2Id,
                Exercise = new Exercise { Name = "Exercise B" },
                Series = 4,
                Reps = 8,
                Weight = 75,
                Unit = WeightUnits.Pounds
            },
            new ExerciseScore
            {
                Id = (LgymApi.Domain.ValueObjects.Id<ExerciseScore>)score3Id,
                Exercise = new Exercise { Name = "Exercise C" },
                Series = 2,
                Reps = 15,
                Weight = 30,
                Unit = WeightUnits.Kilograms
            }
        };

        var command = new TrainingCompletedCommand
        {
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)userId,
            TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)trainingId
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

    [Test]
    public async Task ExecuteAsync_UserNotFound_SkipsSchedulingGracefully()
    {
        // Arrange
         var userId = Id<User>.New();
         var trainingId = Id<Training>.New();

        _testUserRepository.UserToReturn = null;

        var command = new TrainingCompletedCommand
        {
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)userId,
            TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)trainingId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testScheduler.ScheduledPayloads, Is.Empty);
        Assert.That(_testLogger.WarningMessages, Has.Count.EqualTo(1));
        Assert.That(_testLogger.WarningMessages[0], Does.Contain("user"));
        Assert.That(_testLogger.WarningMessages[0], Does.Contain("not found"));
    }

    [Test]
    public async Task ExecuteAsync_TrainingNotFound_SkipsSchedulingGracefully()
    {
        // Arrange
         var userId = Id<User>.New();
         var trainingId = Id<Training>.New();

        _testUserRepository.UserToReturn = new User
        {
            Id = (LgymApi.Domain.ValueObjects.Id<User>)userId,
            Email = "user@example.com",
            PreferredLanguage = "en-US"
        };

        _testTrainingRepository.TrainingToReturn = null;

        var command = new TrainingCompletedCommand
        {
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)userId,
            TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)trainingId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testScheduler.ScheduledPayloads, Is.Empty);
        Assert.That(_testLogger.WarningMessages, Has.Count.EqualTo(1));
        Assert.That(_testLogger.WarningMessages[0], Does.Contain("training"));
        Assert.That(_testLogger.WarningMessages[0], Does.Contain("not found"));
    }

    [Test]
    public async Task ExecuteAsync_UserNotSubscribed_SkipsSchedulingGracefully()
    {
        // Arrange
         var userId = Id<User>.New();
         var trainingId = Id<Training>.New();

        _testUserRepository.UserToReturn = new User
        {
            Id = (LgymApi.Domain.ValueObjects.Id<User>)userId,
            Email = "user@example.com",
            PreferredLanguage = "en-US"
        };

        _testTrainingRepository.TrainingToReturn = new Training
        {
            Id = (LgymApi.Domain.ValueObjects.Id<Training>)trainingId,
            PlanDay = new PlanDay { Name = "Day 1" },
            CreatedAt = DateTimeOffset.UtcNow
        };

        _testSubscriptionRepository.IsSubscribed = false;

        var command = new TrainingCompletedCommand
        {
            UserId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.User>)userId,
            TrainingId = (LgymApi.Domain.ValueObjects.Id<LgymApi.Domain.Entities.Training>)trainingId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testScheduler.ScheduledPayloads, Is.Empty);
        Assert.That(_testLogger.InformationMessages, Has.Count.EqualTo(1));
        Assert.That(_testLogger.InformationMessages[0], Does.Contain("subscription is disabled"));
    }

    // Test doubles
    private sealed class TestUserRepository : IUserRepository
    {
        public User? UserToReturn { get; set; }

        public Task<User?> FindByIdAsync(Id<LgymApi.Domain.Entities.User> id, CancellationToken cancellationToken = default)
            => Task.FromResult(UserToReturn);

        public Task<User?> FindByIdIncludingDeletedAsync(Id<LgymApi.Domain.Entities.User> id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

         public Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default)
             => throw new NotSupportedException();

        public Task<User?> FindByNameOrEmailAsync(string name, string email, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<UserRankingEntry>> GetRankingAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TestTrainingRepository : ITrainingRepository
    {
        public Training? TrainingToReturn { get; set; }

        public Task<Training?> GetByIdAsync(Id<Training> trainingId, CancellationToken cancellationToken = default)
            => Task.FromResult(TrainingToReturn);

        public Task AddAsync(Training training, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Training?> GetLastByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<Training>> GetByUserIdAndDateAsync(Id<User> userId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<DateTimeOffset>> GetDatesByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<Training>> GetByGymIdsAsync(List<Id<Gym>> gymIds, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<Training>> GetByPlanDayIdsAsync(List<Id<PlanDay>> planDayIds, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TestTrainingExerciseScoreRepository : ITrainingExerciseScoreRepository
    {
        public List<TrainingExerciseScore> TrainingExercisesToReturn { get; set; } = new();

        public Task<List<TrainingExerciseScore>> GetByTrainingIdsAsync(
            List<Id<Training>> trainingIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult(TrainingExercisesToReturn);

        public Task AddRangeAsync(IEnumerable<TrainingExerciseScore> trainingExerciseScores, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TestExerciseScoreRepository : IExerciseScoreRepository
    {
        public List<ExerciseScore> ExerciseScoresToReturn { get; set; } = new();

        public Task<List<ExerciseScore>> GetByIdsAsync(
            List<Id<ExerciseScore>> exerciseScoreIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ExerciseScoresToReturn);

        public Task AddRangeAsync(IEnumerable<ExerciseScore> scores, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<ExerciseScore>> GetByUserAndExerciseAsync(Id<User> userId, Id<Exercise> exerciseId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<ExerciseScore>> GetByUserAndExerciseAndGymAsync(Id<User> userId, Id<Exercise> exerciseId, Id<Gym>? gymId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<ExerciseScore>> GetByUserAndExercisesAsync(Id<User> userId, List<Id<Exercise>> exerciseIds, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<ExerciseScore>> GetLatestByUserExerciseSeriesAsync(Id<User> userId, Id<Exercise> exerciseId, Id<Gym>? gymId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ExerciseScore?> GetLatestByUserExerciseSeriesAsync(Id<User> userId, Id<Exercise> exerciseId, int series, Id<Gym>? gymId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ExerciseScore?> GetBestScoreAsync(Id<User> userId, Id<Exercise> exerciseId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

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

    private sealed class TestEmailNotificationSubscriptionRepository : IEmailNotificationSubscriptionRepository
    {
        public bool IsSubscribed { get; set; } = true;

        public Task<bool> IsSubscribedAsync(Id<User> userId, string notificationType, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(IsSubscribed);
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
