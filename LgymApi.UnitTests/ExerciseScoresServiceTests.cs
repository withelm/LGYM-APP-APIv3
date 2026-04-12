using FluentAssertions;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.AdminManagement.Models;
using LgymApi.Application.Features.ExerciseScores;
using LgymApi.Application.Models;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExerciseScoresServiceTests
{
    [Test]
    public async Task GetExerciseScoresChartDataAsync_WithEmptyUserId_ReturnsInvalidExerciseScoreError()
    {
        var service = new ExerciseScoresService(
            new NoOpUserRepository(),
            new NoOpExerciseScoreRepository());

        var exerciseId = Id<Exercise>.New();
        var result = await service.GetExerciseScoresChartDataAsync(Id<User>.Empty, exerciseId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidExerciseScoreError>();
    }

    [Test]
    public async Task GetExerciseScoresChartDataAsync_WithEmptyExerciseId_ReturnsInvalidExerciseScoreError()
    {
        var service = new ExerciseScoresService(
            new NoOpUserRepository(),
            new NoOpExerciseScoreRepository());

        var userId = Id<User>.New();
        var result = await service.GetExerciseScoresChartDataAsync(userId, Id<Exercise>.Empty);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidExerciseScoreError>();
    }

    private sealed class NoOpUserRepository : IUserRepository
    {
        public Task<User?> FindByIdAsync(Id<User> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByIdIncludingDeletedAsync(Id<User> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByNameOrEmailAsync(string name, string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<LgymApi.Application.Models.UserRankingEntry>> GetRankingAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Pagination<UserResult>> GetUsersPaginatedAsync(FilterInput filterInput, bool includeDeleted, CancellationToken cancellationToken = default)
            => Task.FromResult(new Pagination<UserResult>());
    }

    private sealed class NoOpExerciseScoreRepository : IExerciseScoreRepository
    {
        public Task AddRangeAsync(IEnumerable<ExerciseScore> scores, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<ExerciseScore>> GetByIdsAsync(List<Id<ExerciseScore>> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<ExerciseScore>> GetByUserAndExerciseAsync(Id<User> userId, Id<Exercise> exerciseId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<ExerciseScore>> GetByUserAndExerciseAndGymAsync(Id<User> userId, Id<Exercise> exerciseId, Id<Gym>? gymId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<ExerciseScore>> GetByUserAndExercisesAsync(Id<User> userId, List<Id<Exercise>> exerciseIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<ExerciseScore>> GetLatestByUserExerciseSeriesAsync(Id<User> userId, Id<Exercise> exerciseId, Id<Gym>? gymId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExerciseScore?> GetLatestByUserExerciseSeriesAsync(Id<User> userId, Id<Exercise> exerciseId, int series, Id<Gym>? gymId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExerciseScore?> GetBestScoreAsync(Id<User> userId, Id<Exercise> exerciseId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
