using FluentAssertions;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.EloRegistry;
using LgymApi.Application.Features.User;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class EloRegistryServiceTests
{
    [Test]
    public async Task GetChartAsync_WithEmptyUserId_ReturnsInvalidEloRegistryError()
    {
        var service = new EloRegistryService(
            new NoOpEloRegistryRepository(),
            Substitute.For<IUserService>(),
            Substitute.For<IUnitOfWork>());

        var result = await service.GetChartAsync(Id<User>.Empty);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidEloRegistryError>();
    }

    [Test]
    public async Task GetUserEloAsync_WithEmptyUserId_ReturnsInvalidUserError()
    {
        var service = new EloRegistryService(
            new NoOpEloRegistryRepository(),
            Substitute.For<IUserService>(),
            Substitute.For<IUnitOfWork>());

        var result = await service.GetUserEloAsync(Id<User>.Empty);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidUserError>();
    }

    [Test]
    public async Task GetChartAsync_WhenEntriesExist_PreservesRepositoryOrder()
    {
        var userId = Id<User>.New();
        var entries = new List<EloRegistry>
        {
            new()
            {
                Id = Id<EloRegistry>.New(),
                UserId = userId,
                Elo = 1020,
                Date = new DateTimeOffset(2026, 2, 12, 8, 0, 0, TimeSpan.Zero)
            },
            new()
            {
                Id = Id<EloRegistry>.New(),
                UserId = userId,
                Elo = 1010,
                Date = new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero)
            }
        };
        var eloRepository = Substitute.For<IEloRegistryRepository>();
        eloRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(entries);
        var service = new EloRegistryService(
            eloRepository,
            Substitute.For<IUserService>(),
            Substitute.For<IUnitOfWork>());

        var result = await service.GetChartAsync(userId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(entry => entry.Id).Should().Equal(entries.Select(entry => entry.Id));
        result.Value.Select(entry => entry.Value).Should().Equal(1020, 1010);
        result.Value.Select(entry => entry.Date).Should().Equal("02/12", "01/10");
    }

    private sealed class NoOpEloRegistryRepository : IEloRegistryRepository
    {
        public Task<List<EloRegistry>> GetByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(EloRegistry eloRegistry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CreateInitialForUserAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int?> GetLatestEloAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EloRegistry?> GetLatestEntryAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
