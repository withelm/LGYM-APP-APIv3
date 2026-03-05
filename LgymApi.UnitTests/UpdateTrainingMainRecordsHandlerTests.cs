using LgymApi.Application.Repositories;
using LgymApi.Application.Units;
using LgymApi.Application.Models;
using LgymApi.BackgroundWorker.Actions;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Entities;
using Microsoft.Extensions.Logging;
using MainRecordEntity = LgymApi.Domain.Entities.MainRecord;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UpdateTrainingMainRecordsHandlerTests
{
    private TestMainRecordRepository _testMainRecordRepository = null!;
    private TestTrainingRepository _testTrainingRepository = null!;
    private TestTrainingExerciseScoreRepository _testTrainingExerciseScoreRepository = null!;
    private TestExerciseScoreRepository _testExerciseScoreRepository = null!;
    private TestUnitOfWork _testUnitOfWork = null!;
    private TestWeightUnitConverter _testConverter = null!;
    private TestLogger _testLogger = null!;
    private UpdateTrainingMainRecordsHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _testMainRecordRepository = new TestMainRecordRepository();
        _testTrainingRepository = new TestTrainingRepository();
        _testTrainingExerciseScoreRepository = new TestTrainingExerciseScoreRepository();
        _testExerciseScoreRepository = new TestExerciseScoreRepository();
        _testUnitOfWork = new TestUnitOfWork();
        _testConverter = new TestWeightUnitConverter();
        _testLogger = new TestLogger();
        _handler = new UpdateTrainingMainRecordsHandler(
            _testMainRecordRepository,
            _testTrainingRepository,
            _testTrainingExerciseScoreRepository,
            _testExerciseScoreRepository,
            _testUnitOfWork,
            _testConverter,
            _testLogger);
    }

    [Test]
    public async Task ExecuteAsync_WithNewPersonalRecord_CreatesMainRecordEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var exerciseScoreId = Guid.NewGuid();
        var trainingDate = DateTimeOffset.UtcNow.AddDays(-1);

        _testTrainingRepository.TrainingToReturn = new Training
        {
            Id = trainingId,
            CreatedAt = trainingDate
        };

        _testTrainingExerciseScoreRepository.TrainingExercisesToReturn = new List<TrainingExerciseScore>
        {
            new TrainingExerciseScore
            {
                TrainingId = trainingId,
                ExerciseScoreId = exerciseScoreId
            }
        };

        _testExerciseScoreRepository.ExerciseScoresToReturn = new List<ExerciseScore>
        {
            new ExerciseScore
            {
                Id = exerciseScoreId,
                ExerciseId = exerciseId,
                Weight = 90,
                Unit = WeightUnits.Kilograms,
                Series = 3,
                Reps = 10
            }
        };

        _testMainRecordRepository.ExistingRecords = new List<MainRecordEntity>
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
        };

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = trainingId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testMainRecordRepository.AddedRecords, Has.Count.EqualTo(1));
        var newRecord = _testMainRecordRepository.AddedRecords[0];
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
        var trainingId = Guid.NewGuid();
        var exerciseScoreId = Guid.NewGuid();

        _testTrainingRepository.TrainingToReturn = new Training
        {
            Id = trainingId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _testTrainingExerciseScoreRepository.TrainingExercisesToReturn = new List<TrainingExerciseScore>
        {
            new TrainingExerciseScore
            {
                TrainingId = trainingId,
                ExerciseScoreId = exerciseScoreId
            }
        };

        _testExerciseScoreRepository.ExerciseScoresToReturn = new List<ExerciseScore>
        {
            new ExerciseScore
            {
                Id = exerciseScoreId,
                ExerciseId = exerciseId,
                Weight = 90,
                Unit = WeightUnits.Kilograms,
                Series = 3,
                Reps = 10
            }
        };

        _testMainRecordRepository.ExistingRecords = new List<MainRecordEntity>
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
        };

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = trainingId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testMainRecordRepository.AddedRecords, Is.Empty);
    }

    [Test]
    public async Task ExecuteAsync_WithFirstExerciseAttempt_CreatesFirstRecord()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var exerciseScoreId = Guid.NewGuid();
        var trainingDate = DateTimeOffset.UtcNow;

        _testTrainingRepository.TrainingToReturn = new Training
        {
            Id = trainingId,
            CreatedAt = trainingDate
        };

        _testTrainingExerciseScoreRepository.TrainingExercisesToReturn = new List<TrainingExerciseScore>
        {
            new TrainingExerciseScore
            {
                TrainingId = trainingId,
                ExerciseScoreId = exerciseScoreId
            }
        };

        _testExerciseScoreRepository.ExerciseScoresToReturn = new List<ExerciseScore>
        {
            new ExerciseScore
            {
                Id = exerciseScoreId,
                ExerciseId = exerciseId,
                Weight = 120,
                Unit = WeightUnits.Kilograms,
                Series = 3,
                Reps = 8
            }
        };

        _testMainRecordRepository.ExistingRecords = new List<MainRecordEntity>();

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = trainingId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testMainRecordRepository.AddedRecords, Has.Count.EqualTo(1));
        var newRecord = _testMainRecordRepository.AddedRecords[0];
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
        var trainingId = Guid.NewGuid();
        var scoreId1 = Guid.NewGuid();
        var scoreId2 = Guid.NewGuid();
        var trainingDate = DateTimeOffset.UtcNow;

        _testTrainingRepository.TrainingToReturn = new Training
        {
            Id = trainingId,
            CreatedAt = trainingDate
        };

        _testTrainingExerciseScoreRepository.TrainingExercisesToReturn = new List<TrainingExerciseScore>
        {
            new TrainingExerciseScore { TrainingId = trainingId, ExerciseScoreId = scoreId1 },
            new TrainingExerciseScore { TrainingId = trainingId, ExerciseScoreId = scoreId2 }
        };

        _testExerciseScoreRepository.ExerciseScoresToReturn = new List<ExerciseScore>
        {
            new ExerciseScore
            {
                Id = scoreId1,
                ExerciseId = exerciseId1,
                Weight = 100,
                Unit = WeightUnits.Kilograms,
                Series = 4,
                Reps = 8
            },
            new ExerciseScore
            {
                Id = scoreId2,
                ExerciseId = exerciseId2,
                Weight = 80,
                Unit = WeightUnits.Kilograms,
                Series = 3,
                Reps = 10
            }
        };

        _testMainRecordRepository.ExistingRecords = new List<MainRecordEntity>();

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = trainingId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testMainRecordRepository.AddedRecords, Has.Count.EqualTo(2));
        Assert.That(_testMainRecordRepository.AddedRecords.Select(r => r.ExerciseId), 
            Is.EquivalentTo(new[] { exerciseId1, exerciseId2 }));
    }

    [Test]
    public async Task ExecuteAsync_WithUnknownUnit_SkipsExercise()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var validExerciseId = Guid.NewGuid();
        var invalidExerciseId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var validScoreId = Guid.NewGuid();
        var invalidScoreId = Guid.NewGuid();

        _testTrainingRepository.TrainingToReturn = new Training
        {
            Id = trainingId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _testTrainingExerciseScoreRepository.TrainingExercisesToReturn = new List<TrainingExerciseScore>
        {
            new TrainingExerciseScore { TrainingId = trainingId, ExerciseScoreId = invalidScoreId },
            new TrainingExerciseScore { TrainingId = trainingId, ExerciseScoreId = validScoreId }
        };

        _testExerciseScoreRepository.ExerciseScoresToReturn = new List<ExerciseScore>
        {
            new ExerciseScore
            {
                Id = invalidScoreId,
                ExerciseId = invalidExerciseId,
                Weight = 50,
                Unit = WeightUnits.Unknown,
                Series = 3,
                Reps = 10
            },
            new ExerciseScore
            {
                Id = validScoreId,
                ExerciseId = validExerciseId,
                Weight = 60,
                Unit = WeightUnits.Kilograms,
                Series = 3,
                Reps = 10
            }
        };

        _testMainRecordRepository.ExistingRecords = new List<MainRecordEntity>();

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = trainingId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testMainRecordRepository.AddedRecords, Has.Count.EqualTo(1));
        Assert.That(_testMainRecordRepository.AddedRecords[0].ExerciseId, Is.EqualTo(validExerciseId));
    }

    [Test]
    public async Task ExecuteAsync_WithCrossFitConversion_ComparesCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var exerciseScoreId = Guid.NewGuid();

        _testTrainingRepository.TrainingToReturn = new Training
        {
            Id = trainingId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _testTrainingExerciseScoreRepository.TrainingExercisesToReturn = new List<TrainingExerciseScore>
        {
            new TrainingExerciseScore { TrainingId = trainingId, ExerciseScoreId = exerciseScoreId }
        };

        _testExerciseScoreRepository.ExerciseScoresToReturn = new List<ExerciseScore>
        {
            new ExerciseScore
            {
                Id = exerciseScoreId,
                ExerciseId = exerciseId,
                Weight = 220,
                Unit = WeightUnits.Pounds,
                Series = 3,
                Reps = 10
            }
        };

        _testMainRecordRepository.ExistingRecords = new List<MainRecordEntity>
        {
            new MainRecordEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ExerciseId = exerciseId,
                Weight = 90,
                Unit = WeightUnits.Kilograms,
                Date = DateTimeOffset.UtcNow.AddDays(-30)
            }
        };

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = trainingId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert - 220 lbs (~99.8 kg) > 90 kg, so should create new record
        Assert.That(_testMainRecordRepository.AddedRecords, Has.Count.EqualTo(1));
        var newRecord = _testMainRecordRepository.AddedRecords[0];
        Assert.That(newRecord.Weight, Is.EqualTo(220));
        Assert.That(newRecord.Unit, Is.EqualTo(WeightUnits.Pounds));
    }

    [Test]
    public async Task ExecuteAsync_WithMultipleSetsOfSameExercise_RecordsOnlyBest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var scoreId1 = Guid.NewGuid();
        var scoreId2 = Guid.NewGuid();
        var scoreId3 = Guid.NewGuid();

        _testTrainingRepository.TrainingToReturn = new Training
        {
            Id = trainingId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _testTrainingExerciseScoreRepository.TrainingExercisesToReturn = new List<TrainingExerciseScore>
        {
            new TrainingExerciseScore { TrainingId = trainingId, ExerciseScoreId = scoreId1 },
            new TrainingExerciseScore { TrainingId = trainingId, ExerciseScoreId = scoreId2 },
            new TrainingExerciseScore { TrainingId = trainingId, ExerciseScoreId = scoreId3 }
        };

        _testExerciseScoreRepository.ExerciseScoresToReturn = new List<ExerciseScore>
        {
            new ExerciseScore
            {
                Id = scoreId1,
                ExerciseId = exerciseId,
                Weight = 80,
                Unit = WeightUnits.Kilograms,
                Series = 3,
                Reps = 10
            },
            new ExerciseScore
            {
                Id = scoreId2,
                ExerciseId = exerciseId,
                Weight = 90,
                Unit = WeightUnits.Kilograms,
                Series = 3,
                Reps = 8
            },
            new ExerciseScore
            {
                Id = scoreId3,
                ExerciseId = exerciseId,
                Weight = 85,
                Unit = WeightUnits.Kilograms,
                Series = 3,
                Reps = 9
            }
        };

        _testMainRecordRepository.ExistingRecords = new List<MainRecordEntity>();

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = trainingId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert - Only one record for the exercise, with the best weight (90kg)
        Assert.That(_testMainRecordRepository.AddedRecords, Has.Count.EqualTo(1));
        Assert.That(_testMainRecordRepository.AddedRecords[0].Weight, Is.EqualTo(90));
    }

    [Test]
    public async Task ExecuteAsync_TrainingNotFound_SkipsProcessing()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();

        _testTrainingRepository.TrainingToReturn = null;

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = trainingId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testMainRecordRepository.AddedRecords, Is.Empty);
        Assert.That(_testLogger.WarningMessages, Has.Count.EqualTo(1));
        Assert.That(_testLogger.WarningMessages[0], Does.Contain("not found"));
    }

    [Test]
    public async Task ExecuteAsync_NoExerciseScores_SkipsProcessing()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();

        _testTrainingRepository.TrainingToReturn = new Training
        {
            Id = trainingId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _testTrainingExerciseScoreRepository.TrainingExercisesToReturn = new List<TrainingExerciseScore>();

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = trainingId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testMainRecordRepository.AddedRecords, Is.Empty);
        Assert.That(_testLogger.InformationMessages, Has.Count.EqualTo(1));
        Assert.That(_testLogger.InformationMessages[0], Does.Contain("No exercise scores"));
    }

    [Test]
    public async Task ExecuteAsync_WithNewRecords_CallsSaveChanges()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var exerciseScoreId = Guid.NewGuid();

        _testTrainingRepository.TrainingToReturn = new Training
        {
            Id = trainingId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _testTrainingExerciseScoreRepository.TrainingExercisesToReturn = new List<TrainingExerciseScore>
        {
            new TrainingExerciseScore { TrainingId = trainingId, ExerciseScoreId = exerciseScoreId }
        };

        _testExerciseScoreRepository.ExerciseScoresToReturn = new List<ExerciseScore>
        {
            new ExerciseScore
            {
                Id = exerciseScoreId,
                ExerciseId = exerciseId,
                Weight = 100,
                Unit = WeightUnits.Kilograms,
                Series = 3,
                Reps = 10
            }
        };

        _testMainRecordRepository.ExistingRecords = new List<MainRecordEntity>();

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = trainingId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testUnitOfWork.SaveChangesCalled, Is.True);
    }

    [Test]
    public async Task ExecuteAsync_WithNoNewRecords_DoesNotCallSaveChanges()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var trainingId = Guid.NewGuid();
        var exerciseScoreId = Guid.NewGuid();

        _testTrainingRepository.TrainingToReturn = new Training
        {
            Id = trainingId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _testTrainingExerciseScoreRepository.TrainingExercisesToReturn = new List<TrainingExerciseScore>
        {
            new TrainingExerciseScore { TrainingId = trainingId, ExerciseScoreId = exerciseScoreId }
        };

        _testExerciseScoreRepository.ExerciseScoresToReturn = new List<ExerciseScore>
        {
            new ExerciseScore
            {
                Id = exerciseScoreId,
                ExerciseId = exerciseId,
                Weight = 80,
                Unit = WeightUnits.Kilograms,
                Series = 3,
                Reps = 10
            }
        };

        _testMainRecordRepository.ExistingRecords = new List<MainRecordEntity>
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
        };

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            TrainingId = trainingId
        };

        // Act
        await _handler.ExecuteAsync(command);

        // Assert
        Assert.That(_testUnitOfWork.SaveChangesCalled, Is.False);
    }

    [Test]
    public void Constructor_WithNullMainRecordRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new UpdateTrainingMainRecordsHandler(
                null!,
                _testTrainingRepository,
                _testTrainingExerciseScoreRepository,
                _testExerciseScoreRepository,
                _testUnitOfWork,
                _testConverter,
                _testLogger));
        Assert.That(ex.ParamName, Is.EqualTo("mainRecordRepository"));
    }

    [Test]
    public void Constructor_WithNullUnitOfWork_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new UpdateTrainingMainRecordsHandler(
                _testMainRecordRepository,
                _testTrainingRepository,
                _testTrainingExerciseScoreRepository,
                _testExerciseScoreRepository,
                null!,
                _testConverter,
                _testLogger));
        Assert.That(ex.ParamName, Is.EqualTo("unitOfWork"));
    }

    [Test]
    public void Constructor_WithNullConverter_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new UpdateTrainingMainRecordsHandler(
                _testMainRecordRepository,
                _testTrainingRepository,
                _testTrainingExerciseScoreRepository,
                _testExerciseScoreRepository,
                _testUnitOfWork,
                null!,
                _testLogger));
        Assert.That(ex.ParamName, Is.EqualTo("weightUnitConverter"));
    }

    [Test]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new UpdateTrainingMainRecordsHandler(
                _testMainRecordRepository,
                _testTrainingRepository,
                _testTrainingExerciseScoreRepository,
                _testExerciseScoreRepository,
                _testUnitOfWork,
                _testConverter,
                null!));
        Assert.That(ex.ParamName, Is.EqualTo("logger"));
    }

    // Test doubles
    private sealed class TestMainRecordRepository : IMainRecordRepository
    {
        public List<MainRecordEntity> AddedRecords { get; } = new();
        public List<MainRecordEntity> ExistingRecords { get; set; } = new();

        public Task<List<MainRecordEntity>> GetBestByUserGroupedByExerciseAndUnitAsync(
            Guid userId,
            IReadOnlyCollection<Guid>? exerciseIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingRecords);

        public Task AddAsync(MainRecordEntity mainRecord, CancellationToken cancellationToken = default)
        {
            AddedRecords.Add(mainRecord);
            return Task.CompletedTask;
        }

        public Task<List<MainRecordEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<MainRecordEntity>> GetByUserAndExerciseAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<MainRecordEntity>> GetByUserAndExercisesAsync(Guid userId, IReadOnlyCollection<Guid> exerciseIds, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MainRecordEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(MainRecordEntity record, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task UpdateAsync(MainRecordEntity record, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MainRecordEntity?> GetLatestByUserAndExerciseAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TestTrainingRepository : ITrainingRepository
    {
        public Training? TrainingToReturn { get; set; }

        public Task<Training?> GetByIdAsync(Guid trainingId, CancellationToken cancellationToken = default)
            => Task.FromResult(TrainingToReturn);

        public Task AddAsync(Training training, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Training?> GetLastByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<Training>> GetByUserIdAndDateAsync(Guid userId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<DateTimeOffset>> GetDatesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<Training>> GetByGymIdsAsync(List<Guid> gymIds, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<Training>> GetByPlanDayIdsAsync(List<Guid> planDayIds, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TestTrainingExerciseScoreRepository : ITrainingExerciseScoreRepository
    {
        public List<TrainingExerciseScore> TrainingExercisesToReturn { get; set; } = new();

        public Task<List<TrainingExerciseScore>> GetByTrainingIdsAsync(
            List<Guid> trainingIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult(TrainingExercisesToReturn);

        public Task AddRangeAsync(IEnumerable<TrainingExerciseScore> trainingExerciseScores, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TestExerciseScoreRepository : IExerciseScoreRepository
    {
        public List<ExerciseScore> ExerciseScoresToReturn { get; set; } = new();

        public Task<List<ExerciseScore>> GetByIdsAsync(
            List<Guid> exerciseScoreIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ExerciseScoresToReturn);

        public Task AddRangeAsync(IEnumerable<ExerciseScore> scores, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<ExerciseScore>> GetByUserAndExerciseAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<ExerciseScore>> GetByUserAndExerciseAndGymAsync(Guid userId, Guid exerciseId, Guid? gymId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<ExerciseScore>> GetByUserAndExercisesAsync(Guid userId, List<Guid> exerciseIds, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<ExerciseScore>> GetLatestByUserExerciseSeriesAsync(Guid userId, Guid exerciseId, Guid? gymId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ExerciseScore?> GetLatestByUserExerciseSeriesAsync(Guid userId, Guid exerciseId, int series, Guid? gymId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ExerciseScore?> GetBestScoreAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TestUnitOfWork : IUnitOfWork
    {
        public bool SaveChangesCalled { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalled = true;
            return Task.FromResult(1);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void Dispose() { }
    }

    private sealed class TestWeightUnitConverter : IUnitConverter<WeightUnits>
    {
        public double Convert(double value, WeightUnits from, WeightUnits to)
        {
            if (from == to) return value;
            
            // Simple conversion: 1 kg = 2.20462 lbs
            if (from == WeightUnits.Kilograms && to == WeightUnits.Pounds)
                return value * 2.20462;
            if (from == WeightUnits.Pounds && to == WeightUnits.Kilograms)
                return value / 2.20462;
            
            return value;
        }
    }

    private sealed class TestLogger : ILogger<UpdateTrainingMainRecordsHandler>
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
