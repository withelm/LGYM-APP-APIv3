using LgymApi.Application.Common.Errors;
using FluentAssertions;
using LgymApi.Application.Features.AdminManagement.Models;
using LgymApi.Application.Features.AppConfig;
using LgymApi.Application.Models;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class AppConfigServiceTests
{
    [Test]
    public async Task GetLatestByPlatformAsync_WithUnknownPlatform_ReturnsInvalidAppConfigError()
    {
        var service = new AppConfigService(
            new NoOpUserRepository(),
            new NoOpRoleRepository(),
            new NoOpAppConfigRepository(),
            new NoOpUnitOfWork());

        var result = await service.GetLatestByPlatformAsync(Platforms.Unknown);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidAppConfigError>();
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

    private sealed class NoOpRoleRepository : IRoleRepository
    {
        public Task<List<Role>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Role?> FindByIdAsync(Id<Role> roleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Role?> FindByNameAsync(string roleName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Role>> GetByNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsByNameAsync(string roleName, Id<Role>? excludeRoleId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<string>> GetRoleNamesByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<string>> GetPermissionClaimsByUserIdAsync(Id<User> userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<string>> GetPermissionClaimsByRoleIdAsync(Id<Role> roleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<Id<Role>, List<string>>> GetPermissionClaimsByRoleIdsAsync(IReadOnlyCollection<Id<Role>> roleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UserHasRoleAsync(Id<User> userId, string roleName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UserHasPermissionAsync(Id<User> userId, string permission, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddRoleAsync(Role role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateRoleAsync(Role role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteRoleAsync(Role role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReplaceRolePermissionClaimsAsync(Id<Role> roleId, IReadOnlyCollection<string> permissionClaims, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddUserRolesAsync(Id<User> userId, IReadOnlyCollection<Id<Role>> roleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReplaceUserRolesAsync(Id<User> userId, IReadOnlyCollection<Id<Role>> roleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<Id<User>, List<string>>> GetRoleNamesByUserIdsAsync(IReadOnlyCollection<Id<User>> userIds, CancellationToken cancellationToken = default)
            => Task.FromResult(new Dictionary<Id<User>, List<string>>());
        public Task<Pagination<Role>> GetRolesPaginatedAsync(FilterInput filterInput, CancellationToken cancellationToken = default)
            => Task.FromResult(new Pagination<Role>());
    }

    private sealed class NoOpAppConfigRepository : IAppConfigRepository
    {
        public Task<AppConfig?> GetLatestByPlatformAsync(Platforms platform, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(AppConfig config, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AppConfig?> FindByIdAsync(Id<AppConfig> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Pagination<AppConfig>> GetPaginatedAsync(FilterInput filterInput, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Update(AppConfig config) => throw new NotSupportedException();
        public void Delete(AppConfig config) => throw new NotSupportedException();
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void DetachEntity<TEntity>(TEntity entity) where TEntity : class => throw new NotSupportedException();
    }
}
