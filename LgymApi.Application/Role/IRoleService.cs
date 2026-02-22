using LgymApi.Application.Features.Role.Models;

namespace LgymApi.Application.Features.Role;

public interface IRoleService
{
    Task<List<RoleResult>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<RoleResult> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<RoleResult> CreateRoleAsync(string name, string? description, IReadOnlyCollection<string> permissionClaims, CancellationToken cancellationToken = default);
    Task UpdateRoleAsync(Guid roleId, string name, string? description, IReadOnlyCollection<string> permissionClaims, CancellationToken cancellationToken = default);
    Task DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
    List<PermissionClaimLookupResult> GetAvailablePermissionClaims();
    Task UpdateUserRolesAsync(Guid userId, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default);
}
