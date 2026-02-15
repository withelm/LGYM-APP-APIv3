using LgymApi.Domain.Entities;

namespace LgymApi.Application.Repositories;

public interface IRoleRepository
{
    Task<Role?> FindByNameAsync(string roleName, CancellationToken cancellationToken = default);
    Task<List<Role>> GetByNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default);
    Task<List<string>> GetRoleNamesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<string>> GetPermissionClaimsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> UserHasRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
    Task AddUserRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default);
    Task ReplaceUserRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default);
}
