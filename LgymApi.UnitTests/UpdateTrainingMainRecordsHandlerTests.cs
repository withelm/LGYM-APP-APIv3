using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Application.Repositories;
using LgymApi.Application.Units;
using LgymApi.BackgroundWorker.Actions;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Enums;
using Microsoft.Extensions.Logging;
using MainRecordEntity = LgymApi.Domain.Entities.MainRecord;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UpdateTrainingMainRecordsHandlerTests
{
    private TestMainRecordRepository _testRepository = null!;
    private TestUnitOfWork _testUnitOfWork = null!;
    private TestWeightUnitConverter _testConverter = null!;
    private TestLogger _testLogger = null!;
    private UpdateTrainingMainRecordsHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _testRepository = new TestMainRecordRepository();
        _testUnitOfWork = new TestUnitOfWork();
        _testConverter = new TestWeightUnitConverter();
        _testLogger = new TestLogger();
        _handler = new UpdateTrainingMainRecordsHandler(_testRepository, _testUnitOfWork, _testConverter, _testLogger);
    }

    [Test]
    public async Task ExecuteAsync_WithNewPersonalRecord_CreatesMainRecordEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var trainingDate = DateTimeOffset.UtcNow.AddDays(-1);

        _testRepository.SetExistingRecords(new List<MainRecordEntity>
        {
            new MainRecordEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ExerciseId = exerciseId,
                Weight = 80,
                Unit = WeightUnits.Kilograms,
                Date = trainingDate.AddDays(-30)
            }
        });

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = Guid.NewGuid(),
            RecipientEmail = "user@example.com",
            CultureName = "en-US",
            PlanDayName = "Day 1",
            TrainingDate = trainingDate,
            Exercises = new[]
            {
                new TrainingExerciseSummary
                {
                    ExerciseId = exerciseId.ToString(),
                    ExerciseName = "Bench Press",
                    Series = 3,
                    Reps = 10,
                    Weight = 90,
                    Unit = WeightUnits.Kilograms
                }
            }
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testRepository.AddedRecords, Has.Count.EqualTo(1));
        var newRecord = _testRepository.AddedRecords[0];
        Assert.That(newRecord.UserId, Is.EqualTo(userId));
        Assert.That(newRecord.ExerciseId, Is.EqualTo(exerciseId));
        Assert.That(newRecord.Weight, Is.EqualTo(90));
        Assert.That(newRecord.Unit, Is.EqualTo(WeightUnits.Kilograms));
        Assert.That(newRecord.Date, Is.EqualTo(trainingDate));
    }

    [Test]
    public async Task ExecuteAsync_WithNoImprovement_DoesNotCreateRecord()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();

        _testRepository.SetExistingRecords(new List<MainRecordEntity>
        {
            new MainRecordEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ExerciseId = exerciseId,
                Weight = 100,
                Unit = WeightUnits.Kilograms,
                Date = DateTimeOffset.UtcNow.AddDays(-30)
            }
        });

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = Guid.NewGuid(),
            RecipientEmail = "user@example.com",
            CultureName = "en-US",
            PlanDayName = "Day 1",
            TrainingDate = DateTimeOffset.UtcNow,
            Exercises = new[]
            {
                new TrainingExerciseSummary
                {
                    ExerciseId = exerciseId.ToString(),
                    ExerciseName = "Bench Press",
                    Series = 3,
                    Reps = 10,
                    Weight = 90,
                    Unit = WeightUnits.Kilograms
                }
            }
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testRepository.AddedRecords, Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_WithFirstExerciseAttempt_CreatesFirstRecord()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var trainingDate = DateTimeOffset.UtcNow;

        _testRepository.SetExistingRecords(new List<MainRecordEntity>());

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = Guid.NewGuid(),
            RecipientEmail = "user@example.com",
            CultureName = "en-US",
            PlanDayName = "Day 1",
            TrainingDate = trainingDate,
            Exercises = new[]
            {
                new TrainingExerciseSummary
                {
                    ExerciseId = exerciseId.ToString(),
                    ExerciseName = "Deadlift",
                    Series = 3,
                    Reps = 8,
                    Weight = 120,
                    Unit = WeightUnits.Kilograms
                }
            }
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testRepository.AddedRecords, Has.Count.EqualTo(1));
        var newRecord = _testRepository.AddedRecords[0];
        Assert.That(newRecord.Weight, Is.EqualTo(120));
        Assert.That(newRecord.ExerciseId, Is.EqualTo(exerciseId));
    }

    [Test]
    public async Task ExecuteAsync_WithMultipleExercises_CreatesMultipleRecords()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var exerciseId1 = Guid.NewGuid();
        var exerciseId2 = Guid.NewGuid();
        var trainingDate = DateTimeOffset.UtcNow;

        _testRepository.SetExistingRecords(new List<MainRecordEntity>());

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = Guid.NewGuid(),
            RecipientEmail = "user@example.com",
            CultureName = "en-US",
            PlanDayName = "Day 1",
            TrainingDate = trainingDate,
            Exercises = new[]
            {
                new TrainingExerciseSummary
                {
                    ExerciseId = exerciseId1.ToString(),
                    ExerciseName = "Squat",
                    Series = 4,
                    Reps = 8,
                    Weight = 100,
                    Unit = WeightUnits.Kilograms
                },
                new TrainingExerciseSummary
                {
                    ExerciseId = exerciseId2.ToString(),
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
        Assert.That(_testRepository.AddedRecords, Has.Count.EqualTo(2));
        Assert.That(_testRepository.AddedRecords.Select(r => r.ExerciseId), 
            Is.EquivalentTo(new[] { exerciseId1, exerciseId2 }));
    }

    [Test]
    public async Task ExecuteAsync_WithInvalidExerciseId_SkipsExercise()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var validExerciseId = Guid.NewGuid();

        _testRepository.SetExistingRecords(new List<MainRecordEntity>());

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = Guid.NewGuid(),
            RecipientEmail = "user@example.com",
            CultureName = "en-US",
            PlanDayName = "Day 1",
            TrainingDate = DateTimeOffset.UtcNow,
            Exercises = new[]
            {
                new TrainingExerciseSummary
                {
                    ExerciseId = "invalid-guid",
                    ExerciseName = "Invalid Exercise",
                    Series = 3,
                    Reps = 10,
                    Weight = 50,
                    Unit = WeightUnits.Kilograms
                },
                new TrainingExerciseSummary
                {
                    ExerciseId = validExerciseId.ToString(),
                    ExerciseName = "Valid Exercise",
                    Series = 3,
                    Reps = 10,
                    Weight = 60,
                    Unit = WeightUnits.Kilograms
                }
            }
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testRepository.AddedRecords, Has.Count.EqualTo(1));
        Assert.That(_testRepository.AddedRecords[0].ExerciseId, Is.EqualTo(validExerciseId));
    }

    [Test]
    public async Task ExecuteAsync_WithUnknownUnit_SkipsExercise()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var validExerciseId = Guid.NewGuid();
        var invalidExerciseId = Guid.NewGuid();

        _testRepository.SetExistingRecords(new List<MainRecordEntity>());

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = Guid.NewGuid(),
            RecipientEmail = "user@example.com",
            CultureName = "en-US",
            PlanDayName = "Day 1",
            TrainingDate = DateTimeOffset.UtcNow,
            Exercises = new[]
            {
                new TrainingExerciseSummary
                {
                    ExerciseId = invalidExerciseId.ToString(),
                    ExerciseName = "Unknown Unit Exercise",
                    Series = 3,
                    Reps = 10,
                    Weight = 50,
                    Unit = WeightUnits.Unknown
                },
                new TrainingExerciseSummary
                {
                    ExerciseId = validExerciseId.ToString(),
                    ExerciseName = "Valid Exercise",
                    Series = 3,
                    Reps = 10,
                    Weight = 60,
                    Unit = WeightUnits.Kilograms
                }
            }
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testRepository.AddedRecords, Has.Count.EqualTo(1));
        Assert.That(_testRepository.AddedRecords[0].ExerciseId, Is.EqualTo(validExerciseId));
    }

    [Test]
    public async Task ExecuteAsync_WithMixedUnits_ConvertsAndCompares()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var trainingDate = DateTimeOffset.UtcNow;

        // Existing record: 100kg
        _testRepository.SetExistingRecords(new List<MainRecordEntity>
        {
            new MainRecordEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ExerciseId = exerciseId,
                Weight = 100,
                Unit = WeightUnits.Kilograms,
                Date = trainingDate.AddDays(-30)
            }
        });

        // Training record: 230 lbs (≈104.3kg, should be new record)
        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = Guid.NewGuid(),
            RecipientEmail = "user@example.com",
            CultureName = "en-US",
            PlanDayName = "Day 1",
            TrainingDate = trainingDate,
            Exercises = new[]
            {
                new TrainingExerciseSummary
                {
                    ExerciseId = exerciseId.ToString(),
                    ExerciseName = "Bench Press",
                    Series = 3,
                    Reps = 10,
                    Weight = 230,
                    Unit = WeightUnits.Pounds
                }
            }
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testRepository.AddedRecords, Has.Count.EqualTo(1));
        var newRecord = _testRepository.AddedRecords[0];
        Assert.That(newRecord.Weight, Is.EqualTo(230));
        Assert.That(newRecord.Unit, Is.EqualTo(WeightUnits.Pounds));
    }

    [Test]
    public async Task ExecuteAsync_WithNoValidExercises_LogsInformationAndReturns()
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
            Exercises = new[]
            {
                new TrainingExerciseSummary
                {
                    ExerciseId = "invalid",
                    ExerciseName = "Invalid",
                    Series = 0,
                    Reps = 0,
                    Weight = 0,
                    Unit = WeightUnits.Unknown
                }
            }
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testRepository.AddedRecords, Is.Empty);
        Assert.That(_testLogger.InformationMessages, Has.Count.EqualTo(1));
        Assert.That(_testLogger.InformationMessages[0], Does.Contain("No valid exercises"));
    }

    [Test]
    public async Task ExecuteAsync_WithCancellationToken_PassesTokenToRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();

        _testRepository.SetExistingRecords(new List<MainRecordEntity>());

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = Guid.NewGuid(),
            RecipientEmail = "user@example.com",
            CultureName = "en-US",
            PlanDayName = "Day 1",
            TrainingDate = DateTimeOffset.UtcNow,
            Exercises = new[]
            {
                new TrainingExerciseSummary
                {
                    ExerciseId = exerciseId.ToString(),
                    ExerciseName = "Squat",
                    Series = 3,
                    Reps = 8,
                    Weight = 100,
                    Unit = WeightUnits.Kilograms
                }
            }
        };

        using var cts = new CancellationTokenSource();

        // Act
        await _handler.ExecuteAsync(command, cts.Token);

        // Assert
        Assert.That(_testRepository.ReceivedToken, Is.EqualTo(cts.Token));
        Assert.That(_testUnitOfWork.ReceivedToken, Is.EqualTo(cts.Token));
    }

    [Test]
    public async Task ExecuteAsync_WithBestScoreInTraining_UsesMaxWeightFromSession()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();

        _testRepository.SetExistingRecords(new List<MainRecordEntity>());

        // Multiple sets of same exercise with different weights
        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = Guid.NewGuid(),
            RecipientEmail = "user@example.com",
            CultureName = "en-US",
            PlanDayName = "Day 1",
            TrainingDate = DateTimeOffset.UtcNow,
            Exercises = new[]
            {
                new TrainingExerciseSummary
                {
                    ExerciseId = exerciseId.ToString(),
                    ExerciseName = "Bench Press",
                    Series = 3,
                    Reps = 10,
                    Weight = 80,
                    Unit = WeightUnits.Kilograms
                },
                new TrainingExerciseSummary
                {
                    ExerciseId = exerciseId.ToString(),
                    ExerciseName = "Bench Press",
                    Series = 3,
                    Reps = 8,
                    Weight = 90,
                    Unit = WeightUnits.Kilograms
                },
                new TrainingExerciseSummary
                {
                    ExerciseId = exerciseId.ToString(),
                    ExerciseName = "Bench Press",
                    Series = 2,
                    Reps = 6,
                    Weight = 85,
                    Unit = WeightUnits.Kilograms
                }
            }
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testRepository.AddedRecords, Has.Count.EqualTo(1));
        Assert.That(_testRepository.AddedRecords[0].Weight, Is.EqualTo(90));
    }

    [Test]
    public async Task ExecuteAsync_LogsInformationWithRecordCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();

        _testRepository.SetExistingRecords(new List<MainRecordEntity>());

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = Guid.NewGuid(),
            RecipientEmail = "user@example.com",
            CultureName = "en-US",
            PlanDayName = "Day 1",
            TrainingDate = DateTimeOffset.UtcNow,
            Exercises = new[]
            {
                new TrainingExerciseSummary
                {
                    ExerciseId = exerciseId.ToString(),
                    ExerciseName = "Squat",
                    Series = 3,
                    Reps = 8,
                    Weight = 100,
                    Unit = WeightUnits.Kilograms
                }
            }
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testLogger.InformationMessages, Has.Count.EqualTo(1));
        Assert.That(_testLogger.InformationMessages[0], Does.Contain("Main records synchronized"));
        Assert.That(_testLogger.InformationMessages[0], Does.Contain("1 new personal records"));
    }

    [Test]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new UpdateTrainingMainRecordsHandler(null!, _testUnitOfWork, _testConverter, _testLogger));
        Assert.That(ex.ParamName, Is.EqualTo("mainRecordRepository"));
    }

    [Test]
    public void Constructor_WithNullUnitOfWork_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new UpdateTrainingMainRecordsHandler(_testRepository, null!, _testConverter, _testLogger));
        Assert.That(ex.ParamName, Is.EqualTo("unitOfWork"));
    }

    [Test]
    public void Constructor_WithNullConverter_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new UpdateTrainingMainRecordsHandler(_testRepository, _testUnitOfWork, null!, _testLogger));
        Assert.That(ex.ParamName, Is.EqualTo("weightUnitConverter"));
    }

    [Test]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new UpdateTrainingMainRecordsHandler(_testRepository, _testUnitOfWork, _testConverter, null!));
        Assert.That(ex.ParamName, Is.EqualTo("logger"));
    }

    // Test doubles
    private sealed class TestMainRecordRepository : IMainRecordRepository
    {
        private List<MainRecordEntity> _existingRecords = new();
        public List<MainRecordEntity> AddedRecords { get; } = new();
        public CancellationToken ReceivedToken { get; private set; }

        public void SetExistingRecords(List<MainRecordEntity> records)
        {
            _existingRecords = records;
        }

        public Task<List<MainRecordEntity>> GetBestByUserGroupedByExerciseAndUnitAsync(Guid userId, IReadOnlyCollection<Guid>? exerciseIds = null, CancellationToken cancellationToken = default)
        {
            ReceivedToken = cancellationToken;
            var results = _existingRecords
                .Where(r => r.UserId == userId && (exerciseIds == null || exerciseIds.Contains(r.ExerciseId)))
                .ToList();
            return Task.FromResult(results);
        }

        public Task AddAsync(MainRecordEntity record, CancellationToken cancellationToken = default)
        {
            AddedRecords.Add(record);
            ReceivedToken = cancellationToken;
            return Task.CompletedTask;
        }

        // IMainRecordRepository interface implementation
        public Task<List<MainRecordEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<List<MainRecordEntity>> GetByUserAndExerciseAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<List<MainRecordEntity>> GetByUserAndExercisesAsync(Guid userId, IReadOnlyCollection<Guid> exerciseIds, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<MainRecordEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task DeleteAsync(MainRecordEntity record, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task UpdateAsync(MainRecordEntity record, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<MainRecordEntity?> GetLatestByUserAndExerciseAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

    }
    private sealed class TestWeightUnitConverter : IUnitConverter<WeightUnits>
    {
        public double Convert(double value, WeightUnits from, WeightUnits to)
        {
            if (from == to) return value;

            // Simple conversion: 1 lb = 0.453592 kg
            if (from == WeightUnits.Pounds && to == WeightUnits.Kilograms)
                return value * 0.453592;
            if (from == WeightUnits.Kilograms && to == WeightUnits.Pounds)
                return value / 0.453592;

            return value;
        }
    }

    private sealed class TestUnitOfWork : IUnitOfWork
    {
        public CancellationToken ReceivedToken { get; private set; }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ReceivedToken = cancellationToken;
            return Task.FromResult(0);
        }
    }

    private sealed class TestLogger : ILogger<UpdateTrainingMainRecordsHandler>
    {
        public List<string> InformationMessages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            if (logLevel == LogLevel.Information)
                InformationMessages.Add(message);
        }
    }
}
