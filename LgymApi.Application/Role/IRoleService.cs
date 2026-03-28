using LgymApi.Application.Features.Role.Models;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Features.Role;

public interface IRoleService
{
    Task<List<RoleResult>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<RoleResult> GetRoleAsync(Id<LgymApi.Domain.Entities.Role> roleId, CancellationToken cancellationToken = default);
    Task<RoleResult> CreateRoleAsync(string name, string? description, IReadOnlyCollection<string> permissionClaims, CancellationToken cancellationToken = default);
    Task UpdateRoleAsync(Id<LgymApi.Domain.Entities.Role> roleId, string name, string? description, IReadOnlyCollection<string> permissionClaims, CancellationToken cancellationToken = default);
    Task DeleteRoleAsync(Id<LgymApi.Domain.Entities.Role> roleId, CancellationToken cancellationToken = default);
    List<PermissionClaimLookupResult> GetAvailablePermissionClaims();
    Task UpdateUserRolesAsync(Id<LgymApi.Domain.Entities.User> userId, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default);
}
