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

    public Task<Role?> FindByNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        return _dbContext.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
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
            .Where(r => normalized.Contains(r.Name.ToLower()))
            .ToListAsync(cancellationToken);
    }

    public Task<List<string>> GetRoleNamesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);
    }

    public Task<List<string>> GetPermissionClaimsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RoleClaims)
            .Where(rc => rc.ClaimType == AuthConstants.PermissionClaimType)
            .Select(rc => rc.ClaimValue)
            .Distinct()
            .OrderBy(value => value)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> UserHasRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.Role.Name == roleName, cancellationToken);
    }

    public Task<bool> UserHasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RoleClaims)
            .AnyAsync(rc => rc.ClaimType == AuthConstants.PermissionClaimType && rc.ClaimValue == permission, cancellationToken);
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
        await _dbContext.SaveChangesAsync(cancellationToken);
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

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
