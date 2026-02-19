using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Repositories;

public sealed class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _dbContext;

    public RoleRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<Role>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Role?> FindByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);
    }

    public Task<Role?> FindByNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        return _dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
    }

    public Task<bool> ExistsByNameAsync(string roleName, Guid? excludeRoleId = null, CancellationToken cancellationToken = default)
    {
        var normalizedName = roleName.Trim().ToLowerInvariant();
        return _dbContext.Roles.AnyAsync(r =>
                r.Name.ToLower() == normalizedName &&
                (!excludeRoleId.HasValue || r.Id != excludeRoleId.Value),
            cancellationToken);
    }

    public Task<List<Role>> GetByNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default)
    {
        if (roleNames.Count == 0)
        {
            return Task.FromResult(new List<Role>());
        }

        var normalized = roleNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return _dbContext.Roles
            .AsNoTracking()
            .Where(r => normalized.Contains(r.Name.ToLower()))
            .ToListAsync(cancellationToken);
    }

    public Task<List<string>> GetRoleNamesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);
    }

    public Task<List<string>> GetPermissionClaimsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RoleClaims)
            .Where(rc => rc.ClaimType == AuthConstants.PermissionClaimType)
            .Select(rc => rc.ClaimValue)
            .Distinct()
            .OrderBy(value => value)
            .ToListAsync(cancellationToken);
    }

    public Task<List<string>> GetPermissionClaimsByRoleIdAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return _dbContext.RoleClaims
            .AsNoTracking()
            .Where(rc => rc.RoleId == roleId && rc.ClaimType == AuthConstants.PermissionClaimType)
            .Select(rc => rc.ClaimValue)
            .Distinct()
            .OrderBy(value => value)
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, List<string>>> GetPermissionClaimsByRoleIdsAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        if (roleIds.Count == 0)
        {
            return new Dictionary<Guid, List<string>>();
        }

        var items = await _dbContext.RoleClaims
            .AsNoTracking()
            .Where(rc => roleIds.Contains(rc.RoleId) && rc.ClaimType == AuthConstants.PermissionClaimType)
            .Select(rc => new { rc.RoleId, rc.ClaimValue })
            .ToListAsync(cancellationToken);

        return items
            .GroupBy(i => i.RoleId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(i => i.ClaimValue)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(v => v, StringComparer.Ordinal)
                    .ToList());
    }

    public Task<bool> UserHasRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserRoles
            .AsNoTracking()
            .AnyAsync(ur => ur.UserId == userId && ur.Role.Name == roleName, cancellationToken);
    }

    public Task<bool> UserHasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RoleClaims)
            .AnyAsync(rc => rc.ClaimType == AuthConstants.PermissionClaimType && rc.ClaimValue == permission, cancellationToken);
    }

    public async Task AddRoleAsync(Role role, CancellationToken cancellationToken = default)
    {
        await _dbContext.Roles.AddAsync(role, cancellationToken);
    }

    public Task UpdateRoleAsync(Role role, CancellationToken cancellationToken = default)
    {
        _dbContext.Roles.Update(role);
        return Task.CompletedTask;
    }

    public Task DeleteRoleAsync(Role role, CancellationToken cancellationToken = default)
    {
        _dbContext.Roles.Remove(role);
        return Task.CompletedTask;
    }

    public async Task ReplaceRolePermissionClaimsAsync(Guid roleId, IReadOnlyCollection<string> permissionClaims, CancellationToken cancellationToken = default)
    {
        var existingClaims = await _dbContext.RoleClaims
            .Where(rc => rc.RoleId == roleId && rc.ClaimType == AuthConstants.PermissionClaimType)
            .ToListAsync(cancellationToken);

        if (existingClaims.Count > 0)
        {
            _dbContext.RoleClaims.RemoveRange(existingClaims);
        }

        var claimsToAdd = permissionClaims
            .Where(pc => !string.IsNullOrWhiteSpace(pc))
            .Select(pc => pc.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(pc => pc, StringComparer.Ordinal)
            .Select(pc => new RoleClaim
            {
                Id = Guid.NewGuid(),
                RoleId = roleId,
                ClaimType = AuthConstants.PermissionClaimType,
                ClaimValue = pc
            })
            .ToList();

        if (claimsToAdd.Count > 0)
        {
            await _dbContext.RoleClaims.AddRangeAsync(claimsToAdd, cancellationToken);
        }

    }

    public async Task AddUserRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        if (roleIds.Count == 0)
        {
            return;
        }

        var existingRoleIds = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);

        var missing = roleIds
            .Where(roleId => !existingRoleIds.Contains(roleId))
            .Select(roleId => new UserRole { UserId = userId, RoleId = roleId })
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        await _dbContext.UserRoles.AddRangeAsync(missing, cancellationToken);
    }

    public async Task ReplaceUserRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _dbContext.UserRoles.RemoveRange(existing);
        }

        if (roleIds.Count > 0)
        {
            var items = roleIds.Distinct().Select(roleId => new UserRole
            {
                UserId = userId,
                RoleId = roleId
            });
            await _dbContext.UserRoles.AddRangeAsync(items, cancellationToken);
        }
    }
}
