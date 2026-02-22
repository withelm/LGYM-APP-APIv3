using LgymApi.Application.Features.Exercise;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExerciseServiceTests
{
    [Test]
    public async Task GetLastExerciseScoresAsync_WithSeriesAboveLimit_ClampsToThirty()
    {
        var exerciseId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var scores = new List<ExerciseScore>
        {
            new() { Id = Guid.NewGuid(), ExerciseId = exerciseId, UserId = userId, Series = 1, Reps = 8, Weight = 80, Unit = WeightUnits.Kilograms },
            new() { Id = Guid.NewGuid(), ExerciseId = exerciseId, UserId = userId, Series = 2, Reps = 6, Weight = 90, Unit = WeightUnits.Kilograms }
        };

        var service = new ExerciseService(
            new NoOpUserRepository(),
            new NoOpExerciseRepository(),
            new StubExerciseScoreRepository(scores),
            new NoOpUnitOfWork());

        var result = await service.GetLastExerciseScoresAsync(userId, userId, exerciseId, 100, null, "Bench press");

        Assert.Multiple(() =>
        {
            Assert.That(result.SeriesScores, Has.Count.EqualTo(30));
            Assert.That(result.SeriesScores[0].Score, Is.Not.Null);
            Assert.That(result.SeriesScores[1].Score, Is.Not.Null);
            Assert.That(result.SeriesScores[^1].Series, Is.EqualTo(30));
        });
    }

    private sealed class StubExerciseScoreRepository : IExerciseScoreRepository
    {
        private readonly List<ExerciseScore> _scores;

        public StubExerciseScoreRepository(List<ExerciseScore> scores)
        {
            _scores = scores;
        }

        public Task<List<ExerciseScore>> GetLatestByUserExerciseSeriesAsync(Guid userId, Guid exerciseId, Guid? gymId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_scores);
        }

        public Task AddRangeAsync(IEnumerable<ExerciseScore> scores, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<ExerciseScore>> GetByIdsAsync(List<Guid> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<ExerciseScore>> GetByUserAndExerciseAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<ExerciseScore>> GetByUserAndExerciseAndGymAsync(Guid userId, Guid exerciseId, Guid? gymId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<ExerciseScore>> GetByUserAndExercisesAsync(Guid userId, List<Guid> exerciseIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExerciseScore?> GetLatestByUserExerciseSeriesAsync(Guid userId, Guid exerciseId, int series, Guid? gymId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExerciseScore?> GetBestScoreAsync(Guid userId, Guid exerciseId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoOpUserRepository : IUserRepository
    {
        public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByNameOrEmailAsync(string name, string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<LgymApi.Application.Models.UserRankingEntry>> GetRankingAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoOpExerciseRepository : IExerciseRepository
    {
        public Task<Exercise?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Exercise>> GetAllForUserAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Exercise>> GetAllGlobalAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Exercise>> GetUserExercisesAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Exercise>> GetByBodyPartAsync(Guid userId, BodyParts bodyPart, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Exercise>> GetByIdsAsync(List<Guid> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<Guid, string>> GetTranslationsAsync(IEnumerable<Guid> exerciseIds, IReadOnlyList<string> cultures, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpsertTranslationAsync(Guid exerciseId, string culture, string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(Exercise exercise, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(Exercise exercise, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
