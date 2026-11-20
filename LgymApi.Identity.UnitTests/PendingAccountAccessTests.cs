using LgymApi.Application.Identity.Access;
using LgymApi.Application.Features.AdminManagement.Models;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using NSubstitute;

namespace LgymApi.Identity.UnitTests;

[TestFixture]
public sealed class PendingAccountAccessTests
{
    [Test]
    public async Task Lookup_IsUnavailableForPendingAccount()
    {
        var user = new User { Id = Id<User>.New(), DeletionState = AccountDeletionState.PendingDeletion };
        var accountId = user.Id.Rebind<AccountReference>();
        var repository = new UserRepositoryFake(user);
        var mapper = Substitute.For<IMapper>();
        var lookup = new AccountLookupService(repository, mapper);

        var lookupResult = await lookup.GetByIdAsync(accountId);

        Assert.That(lookupResult, Is.Null);
    }

    private sealed class UserRepositoryFake(User user) : IUserRepository
    {
        public Task<User?> FindByIdAsync(Id<User> id, CancellationToken cancellationToken = default) => Task.FromResult<User?>(id == user.Id ? user : null);
        public Task<User?> FindByIdIncludingDeletedAsync(Id<User> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByIdWithRolesAsync(Id<User> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User?> FindByNameOrEmailAsync(string name, string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(User value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(User value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Pagination<UserResult>> GetUsersPaginatedAsync(FilterInput filterInput, bool includeDeleted, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
