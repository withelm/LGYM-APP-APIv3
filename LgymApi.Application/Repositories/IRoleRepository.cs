using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface IRoleRepository
{
    Task<List<Role>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Role?> FindByIdAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<Role?> FindByNameAsync(string roleName, CancellationToken cancellationToken = default);
    Task<List<Role>> GetByNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string roleName, Guid? excludeRoleId = null, CancellationToken cancellationToken = default);
    Task<List<string>> GetRoleNamesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<string>> GetPermissionClaimsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<string>> GetPermissionClaimsByRoleIdAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, List<string>>> GetPermissionClaimsByRoleIdsAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default);
    Task<bool> UserHasRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
    Task<bool> UserHasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default);
    Task AddRoleAsync(Role role, CancellationToken cancellationToken = default);
    Task UpdateRoleAsync(Role role, CancellationToken cancellationToken = default);
    Task DeleteRoleAsync(Role role, CancellationToken cancellationToken = default);
    Task ReplaceRolePermissionClaimsAsync(Guid roleId, IReadOnlyCollection<string> permissionClaims, CancellationToken cancellationToken = default);
    Task AddUserRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default);
    Task ReplaceUserRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default);
}
