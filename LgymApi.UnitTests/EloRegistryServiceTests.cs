using FluentAssertions;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.AdminManagement.Models;
using LgymApi.Application.Features.EloRegistry;
using LgymApi.Application.Models;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class EloRegistryServiceTests
{
    [Test]
    public async Task GetChartAsync_WithEmptyUserId_ReturnsInvalidEloRegistryError()
    {
        var service = new EloRegistryService(
            new NoOpUserRepository(),
            new NoOpEloRegistryRepository());

        var result = await service.GetChartAsync(Id<User>.Empty);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidEloRegistryError>();
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

    private sealed class NoOpEloRegistryRepository : IEloRegistryRepository
    {
        public Task<List<EloRegistry>> GetByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(EloRegistry eloRegistry, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int?> GetLatestEloAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EloRegistry?> GetLatestEntryAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
