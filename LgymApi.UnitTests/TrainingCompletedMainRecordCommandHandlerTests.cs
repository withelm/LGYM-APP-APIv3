using LgymApi.Application.Repositories;
using LgymApi.Application.Units;
using LgymApi.BackgroundWorker.Actions;
using LgymApi.BackgroundWorker.Common.Commands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainingCompletedMainRecordCommandHandlerTests
{
    [Test]
    public async Task ExecuteAsync_WhenBestScoresExist_InsertsNewAndImprovedRecords()
    {
        var userId = Guid.NewGuid();
        var exerciseWithoutRecord = Guid.NewGuid();
        var exerciseWithRecord = Guid.NewGuid();
        var createdAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        var repository = new FakeMainRecordRepository
        {
            ExistingRecords = new List<MainRecord>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ExerciseId = exerciseWithRecord,
                    Weight = 80,
                    Unit = WeightUnits.Kilograms,
                    Date = DateTimeOffset.UtcNow.AddDays(-1)
                }
            }
        };

        var handler = new TrainingCompletedMainRecordCommandHandler(
            repository,
            new FakeWeightUnitConverter(),
            new FakeUnitOfWork());

        var command = new TrainingCompletedCommand
        {
            UserId = userId,
            CreatedAtUtc = createdAtUtc,
            Exercises = new[]
            {
                new TrainingExerciseInput
                {
                    ExerciseId = exerciseWithoutRecord.ToString(),
                    Weight = 90,
                    Reps = 5,
                    Series = 1,
                    Unit = WeightUnits.Kilograms
                },
                new TrainingExerciseInput
                {
                    ExerciseId = exerciseWithoutRecord.ToString(),
                    Weight = 210,
                    Reps = 5,
                    Series = 2,
                    Unit = WeightUnits.Pounds
                },
                new TrainingExerciseInput
                {
                    ExerciseId = exerciseWithRecord.ToString(),
                    Weight = 85,
                    Reps = 8,
                    Series = 1,
                    Unit = WeightUnits.Kilograms
                }
            }
        };

        await handler.ExecuteAsync(command, CancellationToken.None);

        Assert.That(repository.AddedRecords, Has.Count.EqualTo(2));
        Assert.That(repository.AddedRecords.Any(r =>
            r.UserId == userId &&
            r.ExerciseId == exerciseWithoutRecord &&
            r.Weight == 210 &&
            r.Unit == WeightUnits.Pounds &&
            r.Date == new DateTimeOffset(createdAtUtc)), Is.True);
        Assert.That(repository.AddedRecords.Any(r =>
            r.UserId == userId &&
            r.ExerciseId == exerciseWithRecord &&
            r.Weight == 85 &&
            r.Unit == WeightUnits.Kilograms &&
            r.Date == new DateTimeOffset(createdAtUtc)), Is.True);
    }

    [Test]
    public async Task ExecuteAsync_WithEmptyExercises_DoesNothing()
    {
        var repository = new FakeMainRecordRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new TrainingCompletedMainRecordCommandHandler(repository, new FakeWeightUnitConverter(), unitOfWork);

        var command = new TrainingCompletedCommand
        {
            UserId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            Exercises = Array.Empty<TrainingExerciseInput>()
        };

        await handler.ExecuteAsync(command, CancellationToken.None);

        Assert.That(repository.GetBestCallCount, Is.EqualTo(0));
        Assert.That(repository.AddedRecords, Is.Empty);
        Assert.That(unitOfWork.SaveCallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task ExecuteAsync_WithOnlyInvalidExercises_DoesNothing()
    {
        var repository = new FakeMainRecordRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new TrainingCompletedMainRecordCommandHandler(repository, new FakeWeightUnitConverter(), unitOfWork);

        var command = new TrainingCompletedCommand
        {
            UserId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            Exercises = new[]
            {
                new TrainingExerciseInput
                {
                    ExerciseId = "not-a-guid",
                    Weight = 100,
                    Reps = 5,
                    Series = 1,
                    Unit = WeightUnits.Kilograms
                },
                new TrainingExerciseInput
                {
                    ExerciseId = Guid.NewGuid().ToString(),
                    Weight = 100,
                    Reps = 5,
                    Series = 1,
                    Unit = WeightUnits.Unknown
                }
            }
        };

        await handler.ExecuteAsync(command, CancellationToken.None);

        Assert.That(repository.GetBestCallCount, Is.EqualTo(0));
        Assert.That(repository.AddedRecords, Is.Empty);
        Assert.That(unitOfWork.SaveCallCount, Is.EqualTo(0));
    }

    private sealed class FakeMainRecordRepository : IMainRecordRepository
    {
        public List<MainRecord> ExistingRecords { get; init; } = new();
        public List<MainRecord> AddedRecords { get; } = new();
        public int GetBestCallCount { get; private set; }

        public Task AddAsync(MainRecord record, CancellationToken cancellationToken = default)
        {
            AddedRecords.Add(record);
            return Task.CompletedTask;
        }

        public Task<List<MainRecord>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<MainRecord>());

        public Task<List<MainRecord>> GetByUserAndExerciseAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<MainRecord>());

        public Task<List<MainRecord>> GetByUserAndExercisesAsync(Guid userId, IReadOnlyCollection<Guid> exerciseIds, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<MainRecord>());

        public Task<List<MainRecord>> GetBestByUserGroupedByExerciseAndUnitAsync(Guid userId, IReadOnlyCollection<Guid>? exerciseIds = null, CancellationToken cancellationToken = default)
        {
            GetBestCallCount++;
            return Task.FromResult(ExistingRecords);
        }

        public Task<MainRecord?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<MainRecord?>(null);

        public Task DeleteAsync(MainRecord record, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(MainRecord record, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<MainRecord?> GetLatestByUserAndExerciseAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default)
            => Task.FromResult<MainRecord?>(null);
    }

    private sealed class FakeWeightUnitConverter : IUnitConverter<WeightUnits>
    {
        public double Convert(double value, WeightUnits fromUnit, WeightUnits toUnit)
        {
            if (fromUnit == toUnit)
            {
                return value;
            }

            if (fromUnit == WeightUnits.Pounds && toUnit == WeightUnits.Kilograms)
            {
                return value / 2.2046226218487757d;
            }

            if (fromUnit == WeightUnits.Kilograms && toUnit == WeightUnits.Pounds)
            {
                return value * 2.2046226218487757d;
            }

            throw new ArgumentOutOfRangeException(nameof(fromUnit));
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCallCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            return Task.FromResult(1);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
