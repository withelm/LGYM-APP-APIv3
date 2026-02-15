using LgymApi.Application.Features.Role.Models;

namespace LgymApi.Application.Features.Role;

public interface IRoleService
{
    Task<List<RoleResult>> GetRolesAsync();
    Task<RoleResult> GetRoleAsync(Guid roleId);
    Task<RoleResult> CreateRoleAsync(string name, string? description, IReadOnlyCollection<string> permissionClaims);
    Task UpdateRoleAsync(Guid roleId, string name, string? description, IReadOnlyCollection<string> permissionClaims);
    Task DeleteRoleAsync(Guid roleId);
    List<PermissionClaimLookupResult> GetAvailablePermissionClaims();
    Task UpdateUserRolesAsync(Guid userId, IReadOnlyCollection<string> roleNames);
}
