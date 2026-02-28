using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.Training;
using LgymApi.Application.Features.Training.Models;
using LgymApi.Application.Models;
using LgymApi.Application.Repositories;
using LgymApi.Application.Services;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Services;
using Microsoft.Extensions.Logging;
using TrainingCompletedCommand = LgymApi.BackgroundWorker.Common.Commands.TrainingCompletedCommand;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class TrainingServiceTests
{
    [Test]
    public async Task AddTrainingAsync_WhenTransactionCommits_EnqueuesTrainingCompletedCommandAfterCommit()
    {
        var userId = Guid.NewGuid();
        var gymId = Guid.NewGuid();
        var planDayId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var createdAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        var transaction = new FakeTransaction();
        var unitOfWork = new FakeUnitOfWork(transaction);
        var commandDispatcher = new FakeCommandDispatcher(() => transaction.CommitCalled);
        var userRepository = new FakeUserRepository(new User
        {
            Id = userId,
            Email = "user@example.com",
            PreferredLanguage = "pl-PL"
        });

        var service = new TrainingService(
            userRepository,
            new FakeGymRepository(new Gym { Id = gymId }),
            new FakeTrainingRepository(),
            new FakeExerciseRepository(new Exercise { Id = exerciseId, Name = "Bench Press" }),
            new FakeExerciseScoreRepository(),
            new FakeTrainingExerciseScoreRepository(),
            new FakePlanDayRepository(new PlanDay { Id = planDayId, Name = "Upper" }),
            new FakeEmailNotificationSubscriptionRepository(true),
            commandDispatcher,
            new FakeEloRegistryRepository(new EloRegistry { Id = Guid.NewGuid(), UserId = userId, Elo = 1200 }),
            new FakeRankService(),
            unitOfWork,
            new FakeLogger<TrainingService>());

        var exercises = new[]
        {
            new TrainingExerciseInput
            {
                ExerciseId = exerciseId.ToString(),
                Series = 1,
                Reps = 10,
                Weight = 100,
                Unit = WeightUnits.Kilograms
            }
        };

        await service.AddTrainingAsync(userId, gymId, planDayId, createdAt, exercises, CancellationToken.None);

        Assert.That(transaction.CommitCalled, Is.True);
        Assert.That(commandDispatcher.EnqueuedCommands, Has.Count.EqualTo(1));
        Assert.That(commandDispatcher.WasCommitCompleteWhenEnqueueCalled, Is.True);

        var command = commandDispatcher.EnqueuedCommands[0];
        Assert.That(command.UserId, Is.EqualTo(userId));
        Assert.That(command.TrainingId, Is.Not.EqualTo(Guid.Empty));
        Assert.That(command.CreatedAtUtc, Is.EqualTo(createdAt));
        Assert.That(command.CultureName, Is.EqualTo("pl-PL"));
        Assert.That(command.RecipientEmail, Is.EqualTo("user@example.com"));
        Assert.That(command.PlanDayName, Is.EqualTo("Upper"));
        Assert.That(command.Exercises, Has.Count.EqualTo(1));
        Assert.That(command.ExerciseDetails, Has.Count.EqualTo(1));
        Assert.That(command.ExerciseDetails[0].ExerciseName, Is.EqualTo("Bench Press"));
    }

    [Test]
    public void AddTrainingAsync_WhenSaveChangesFails_DoesNotEnqueueTrainingCompletedCommand()
    {
        var userId = Guid.NewGuid();
        var gymId = Guid.NewGuid();
        var planDayId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();

        var transaction = new FakeTransaction();
        var unitOfWork = new FakeUnitOfWork(transaction) { ThrowOnSaveChanges = true };
        var commandDispatcher = new FakeCommandDispatcher(() => transaction.CommitCalled);

        var service = new TrainingService(
            new FakeUserRepository(new User { Id = userId, Email = "user@example.com" }),
            new FakeGymRepository(new Gym { Id = gymId }),
            new FakeTrainingRepository(),
            new FakeExerciseRepository(new Exercise { Id = exerciseId, Name = "Deadlift" }),
            new FakeExerciseScoreRepository(),
            new FakeTrainingExerciseScoreRepository(),
            new FakePlanDayRepository(new PlanDay { Id = planDayId, Name = "Lower" }),
            new FakeEmailNotificationSubscriptionRepository(true),
            commandDispatcher,
            new FakeEloRegistryRepository(new EloRegistry { Id = Guid.NewGuid(), UserId = userId, Elo = 1500 }),
            new FakeRankService(),
            unitOfWork,
            new FakeLogger<TrainingService>());

        var exercises = new[]
        {
            new TrainingExerciseInput
            {
                ExerciseId = exerciseId.ToString(),
                Series = 1,
                Reps = 5,
                Weight = 120,
                Unit = WeightUnits.Kilograms
            }
        };

        Assert.ThrowsAsync<AppException>(async () =>
            await service.AddTrainingAsync(userId, gymId, planDayId, DateTime.UtcNow, exercises, CancellationToken.None));

        Assert.That(transaction.CommitCalled, Is.False);
        Assert.That(transaction.RollbackCalled, Is.True);
        Assert.That(commandDispatcher.EnqueuedCommands, Is.Empty);
    }

    private sealed class FakeCommandDispatcher : ICommandDispatcher
    {
        private readonly Func<bool> _isCommitted;

        public FakeCommandDispatcher(Func<bool> isCommitted)
        {
            _isCommitted = isCommitted;
        }

        public List<TrainingCompletedCommand> EnqueuedCommands { get; } = new();

        public bool WasCommitCompleteWhenEnqueueCalled { get; private set; }

        public void Enqueue<TCommand>(TCommand command)
            where TCommand : IActionCommand
        {
            WasCommitCompleteWhenEnqueueCalled = _isCommitted();

            if (command is TrainingCompletedCommand typedCommand)
            {
                EnqueuedCommands.Add(typedCommand);
            }
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        private readonly FakeTransaction _transaction;

        public FakeUnitOfWork(FakeTransaction transaction)
        {
            _transaction = transaction;
        }

        public bool ThrowOnSaveChanges { get; init; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnSaveChanges)
            {
                throw new InvalidOperationException("save failed");
            }

            return Task.FromResult(1);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IUnitOfWorkTransaction>(_transaction);
    }

    private sealed class FakeTransaction : IUnitOfWorkTransaction
    {
        public bool CommitCalled { get; private set; }

        public bool RollbackCalled { get; private set; }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCalled = true;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCalled = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly User _user;

        public FakeUserRepository(User user)
        {
            _user = user;
        }

        public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<User?>(_user.Id == id ? _user : null);

        public Task<User?> FindByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<User?>(null);

        public Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult<User?>(null);

        public Task<User?> FindByNameOrEmailAsync(string name, string email, CancellationToken cancellationToken = default)
            => Task.FromResult<User?>(null);

        public Task<List<UserRankingEntry>> GetRankingAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<UserRankingEntry>());

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeGymRepository : IGymRepository
    {
        private readonly Gym _gym;

        public FakeGymRepository(Gym gym)
        {
            _gym = gym;
        }

        public Task AddAsync(Gym gym, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Gym?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Gym?>(_gym.Id == id ? _gym : null);

        public Task<List<Gym>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<Gym>());

        public Task UpdateAsync(Gym gym, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeTrainingRepository : ITrainingRepository
    {
        public Task AddAsync(Training training, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Training?> GetLastByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<Training?>(null);

        public Task<List<Training>> GetByUserIdAndDateAsync(Guid userId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<Training>());

        public Task<List<DateTimeOffset>> GetDatesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<DateTimeOffset>());

        public Task<List<Training>> GetByGymIdsAsync(List<Guid> gymIds, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<Training>());

        public Task<List<Training>> GetByPlanDayIdsAsync(List<Guid> planDayIds, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<Training>());
    }

    private sealed class FakeExerciseRepository : IExerciseRepository
    {
        private readonly Exercise _exercise;

        public FakeExerciseRepository(Exercise exercise)
        {
            _exercise = exercise;
        }

        public Task<Exercise?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Exercise?>(_exercise.Id == id ? _exercise : null);

        public Task<List<Exercise>> GetAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<Exercise>());

        public Task<List<Exercise>> GetAllGlobalAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<Exercise>());

        public Task<List<Exercise>> GetUserExercisesAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<Exercise>());

        public Task<List<Exercise>> GetByBodyPartAsync(Guid userId, BodyParts bodyPart, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<Exercise>());

        public Task<List<Exercise>> GetByIdsAsync(List<Guid> ids, CancellationToken cancellationToken = default)
        {
            var exercises = ids.Contains(_exercise.Id)
                ? new List<Exercise> { _exercise }
                : new List<Exercise>();
            return Task.FromResult(exercises);
        }

        public Task<Dictionary<Guid, string>> GetTranslationsAsync(IEnumerable<Guid> exerciseIds, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default)
            => Task.FromResult(new Dictionary<Guid, string>());

        public Task UpsertTranslationAsync(Guid exerciseId, string culture, string name, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddAsync(Exercise exercise, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(Exercise exercise, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeExerciseScoreRepository : IExerciseScoreRepository
    {
        public Task AddRangeAsync(IEnumerable<ExerciseScore> scores, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<List<ExerciseScore>> GetByIdsAsync(List<Guid> ids, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ExerciseScore>());

        public Task<List<ExerciseScore>> GetByUserAndExerciseAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ExerciseScore>());

        public Task<List<ExerciseScore>> GetByUserAndExerciseAndGymAsync(Guid userId, Guid exerciseId, Guid? gymId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ExerciseScore>());

        public Task<List<ExerciseScore>> GetByUserAndExercisesAsync(Guid userId, List<Guid> exerciseIds, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ExerciseScore>());

        public Task<List<ExerciseScore>> GetLatestByUserExerciseSeriesAsync(Guid userId, Guid exerciseId, Guid? gymId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ExerciseScore>());

        public Task<ExerciseScore?> GetLatestByUserExerciseSeriesAsync(Guid userId, Guid exerciseId, int series, Guid? gymId, CancellationToken cancellationToken = default)
            => Task.FromResult<ExerciseScore?>(null);

        public Task<ExerciseScore?> GetBestScoreAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default)
            => Task.FromResult<ExerciseScore?>(null);
    }

    private sealed class FakeTrainingExerciseScoreRepository : ITrainingExerciseScoreRepository
    {
        public Task AddRangeAsync(IEnumerable<TrainingExerciseScore> scores, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<List<TrainingExerciseScore>> GetByTrainingIdsAsync(List<Guid> trainingIds, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<TrainingExerciseScore>());
    }

    private sealed class FakePlanDayRepository : IPlanDayRepository
    {
        private readonly PlanDay _planDay;

        public FakePlanDayRepository(PlanDay planDay)
        {
            _planDay = planDay;
        }

        public Task<PlanDay?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<PlanDay?>(_planDay.Id == id ? _planDay : null);

        public Task<List<PlanDay>> GetByPlanIdAsync(Guid planId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<PlanDay>());

        public Task AddAsync(PlanDay planDay, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(PlanDay planDay, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarkDeletedAsync(Guid planDayId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarkDeletedByPlanIdAsync(Guid planId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> AnyByPlanIdAsync(Guid planId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class FakeEmailNotificationSubscriptionRepository : IEmailNotificationSubscriptionRepository
    {
        private readonly bool _isSubscribed;

        public FakeEmailNotificationSubscriptionRepository(bool isSubscribed)
        {
            _isSubscribed = isSubscribed;
        }

        public Task<bool> IsSubscribedAsync(Guid userId, string notificationType, CancellationToken cancellationToken = default)
            => Task.FromResult(_isSubscribed);
    }

    private sealed class FakeEloRegistryRepository : IEloRegistryRepository
    {
        private readonly EloRegistry _latestEntry;

        public FakeEloRegistryRepository(EloRegistry latestEntry)
        {
            _latestEntry = latestEntry;
        }

        public Task AddAsync(EloRegistry registry, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int?> GetLatestEloAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<int?>(_latestEntry.Elo);

        public Task<EloRegistry?> GetLatestEntryAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<EloRegistry?>(_latestEntry);

        public Task<List<EloRegistry>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<EloRegistry>());
    }

    private sealed class FakeRankService : IRankService
    {
        private static readonly RankDefinition CurrentRank = new() { Name = "Junior 2", NeedElo = 1001 };
        private static readonly RankDefinition NextRank = new() { Name = "Junior 3", NeedElo = 2500 };

        public IReadOnlyList<RankDefinition> GetRanks() => new[] { CurrentRank, NextRank };

        public RankDefinition GetCurrentRank(int elo) => CurrentRank;

        public RankDefinition? GetNextRank(string currentRankName) => NextRank;
    }

    private sealed class FakeLogger<TCategory> : ILogger<TCategory>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}
