using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.Role.Models;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Resources;

namespace LgymApi.Application.Features.Role;

public sealed class RoleService : IRoleService
{
    private static readonly HashSet<string> SystemRoles =
    [
        AuthConstants.Roles.User,
        AuthConstants.Roles.Admin,
        AuthConstants.Roles.Tester
    ];

    private readonly IRoleRepository _roleRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RoleService(IRoleRepository roleRepository, IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _roleRepository = roleRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<RoleResult>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _roleRepository.GetAllAsync(cancellationToken);
        var claimsByRole = await _roleRepository.GetPermissionClaimsByRoleIdsAsync(roles.Select(r => r.Id).ToList(), cancellationToken);

        return roles
            .Select(role => MapRole(
                role,
                claimsByRole.TryGetValue(role.Id, out var claims) ? claims : new List<string>()))
            .ToList();
    }

    public async Task<RoleResult> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        if (roleId == Guid.Empty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var role = await _roleRepository.FindByIdAsync(roleId, cancellationToken);
        if (role == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var permissionClaims = await _roleRepository.GetPermissionClaimsByRoleIdAsync(role.Id, cancellationToken);
        return MapRole(role, permissionClaims);
    }

    public async Task<RoleResult> CreateRoleAsync(string name, string? description, IReadOnlyCollection<string> permissionClaims, CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeRoleName(name);
        var normalizedClaims = NormalizeAndValidateClaims(permissionClaims);

        if (await _roleRepository.ExistsByNameAsync(normalizedName, cancellationToken: cancellationToken))
        {
            throw AppException.BadRequest(Messages.RoleWithThatName);
        }

        var role = new Domain.Entities.Role
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Description = NormalizeDescription(description)
        };

        await _roleRepository.AddRoleAsync(role, cancellationToken);
        await _roleRepository.ReplaceRolePermissionClaimsAsync(role.Id, normalizedClaims, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RoleResult
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            PermissionClaims = normalizedClaims.ToList()
        };
    }

    public async Task UpdateRoleAsync(Guid roleId, string name, string? description, IReadOnlyCollection<string> permissionClaims, CancellationToken cancellationToken = default)
    {
        if (roleId == Guid.Empty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var role = await _roleRepository.FindByIdAsync(roleId, cancellationToken);
        if (role == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (SystemRoles.Contains(role.Name))
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        var normalizedName = NormalizeRoleName(name);
        var normalizedClaims = NormalizeAndValidateClaims(permissionClaims);

        if (await _roleRepository.ExistsByNameAsync(normalizedName, roleId, cancellationToken))
        {
            throw AppException.BadRequest(Messages.RoleWithThatName);
        }

        role.Name = normalizedName;
        role.Description = NormalizeDescription(description);

        await _roleRepository.UpdateRoleAsync(role, cancellationToken);
        await _roleRepository.ReplaceRolePermissionClaimsAsync(role.Id, normalizedClaims, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        if (roleId == Guid.Empty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var role = await _roleRepository.FindByIdAsync(roleId, cancellationToken);
        if (role == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        if (SystemRoles.Contains(role.Name))
        {
            throw AppException.Forbidden(Messages.Forbidden);
        }

        await _roleRepository.DeleteRoleAsync(role, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public List<PermissionClaimLookupResult> GetAvailablePermissionClaims()
    {
        return AuthConstants.Permissions.All
            .OrderBy(claim => claim, StringComparer.Ordinal)
            .Select(claim => new PermissionClaimLookupResult
            {
                ClaimType = AuthConstants.PermissionClaimType,
                ClaimValue = claim,
                DisplayName = claim switch
                {
                    AuthConstants.Permissions.AdminAccess => Messages.PermissionAdminAccess,
                    AuthConstants.Permissions.ManageUserRoles => Messages.PermissionManageUserRoles,
                    AuthConstants.Permissions.ManageAppConfig => Messages.PermissionManageAppConfig,
                    AuthConstants.Permissions.ManageGlobalExercises => Messages.PermissionManageGlobalExercises,
                    _ => claim
                }
            })
            .ToList();
    }

    public async Task UpdateUserRolesAsync(Guid userId, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        var user = await _userRepository.FindByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw AppException.NotFound(Messages.DidntFind);
        }

        var normalizedRoleNames = roleNames
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rolesToSet = await _roleRepository.GetByNamesAsync(normalizedRoleNames, cancellationToken);
        if (rolesToSet.Count != normalizedRoleNames.Count)
        {
            throw AppException.BadRequest(Messages.InvalidRoleSelection);
        }

        await _roleRepository.ReplaceUserRolesAsync(userId, rolesToSet.Select(r => r.Id).ToList(), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static RoleResult MapRole(Domain.Entities.Role role, List<string> permissionClaims)
    {
        return new RoleResult
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            PermissionClaims = permissionClaims
        };
    }

    private static string NormalizeRoleName(string name)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw AppException.BadRequest(Messages.FieldRequired);
        }

        return normalizedName;
    }

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static List<string> NormalizeAndValidateClaims(IReadOnlyCollection<string> permissionClaims)
    {
        var normalizedClaims = permissionClaims
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        if (normalizedClaims.Any(c => !AuthConstants.Permissions.All.Contains(c, StringComparer.Ordinal)))
        {
            throw AppException.BadRequest(Messages.InvalidPermissionClaims);
        }

        return normalizedClaims;
    }
}
