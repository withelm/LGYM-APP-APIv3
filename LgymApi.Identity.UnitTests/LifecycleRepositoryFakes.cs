using LgymApi.Application.Features.AdminManagement.Models;
using LgymApi.Application.Pagination;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Identity.UnitTests;

internal sealed class LifecycleUserRepository(User user) : IUserRepository
{
    public Task<User?> FindByIdIncludingDeletedAsync(Id<User> id, CancellationToken cancellationToken = default) => Task.FromResult<User?>(id == user.Id ? user : null);
    public Task<User?> FindByIdAsync(Id<User> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<User?> FindByIdWithRolesAsync(Id<User> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<User?> FindByNameOrEmailAsync(string name, string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task AddAsync(User value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task UpdateAsync(User value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Pagination<UserResult>> GetUsersPaginatedAsync(FilterInput filterInput, bool includeDeleted, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

internal sealed class LifecycleRoleRepository : IRoleRepository
{
    public Task<List<string>> GetRoleNamesByUserIdAsync(Id<User> id, CancellationToken cancellationToken = default) => Task.FromResult(new List<string> { "Admin" });
    public Task<List<string>> GetPermissionClaimsByUserIdAsync(Id<User> id, CancellationToken cancellationToken = default) => Task.FromResult(new List<string> { AuthConstants.Permissions.AdminAccess });
    public Task<List<Role>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Role?> FindByIdAsync(Id<Role> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Role?> FindByNameAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<List<Role>> GetByNamesAsync(IReadOnlyCollection<string> names, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> ExistsByNameAsync(string name, Id<Role>? excludeRoleId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Dictionary<Id<User>, List<string>>> GetRoleNamesByUserIdsAsync(IReadOnlyCollection<Id<User>> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<List<string>> GetPermissionClaimsByRoleIdAsync(Id<Role> id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Dictionary<Id<Role>, List<string>>> GetPermissionClaimsByRoleIdsAsync(IReadOnlyCollection<Id<Role>> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> UserHasRoleAsync(Id<User> id, string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> UserHasPermissionAsync(Id<User> id, string permission, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task AddRoleAsync(Role role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task UpdateRoleAsync(Role role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteRoleAsync(Role role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task ReplaceRolePermissionClaimsAsync(Id<Role> id, IReadOnlyCollection<string> claims, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task AddUserRolesAsync(Id<User> id, IReadOnlyCollection<Id<Role>> roles, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task ReplaceUserRolesAsync(Id<User> id, IReadOnlyCollection<Id<Role>> roles, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Pagination<Role>> GetRolesPaginatedAsync(FilterInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
