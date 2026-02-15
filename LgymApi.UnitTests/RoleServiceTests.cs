using System.Net;
using LgymApi.Application.Exceptions;
using LgymApi.Application.Features.Role;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class RoleServiceTests
{
    private RoleService _service = null!;
    private InMemoryRoleRepository _roleRepository = null!;
    private InMemoryUserRepository _userRepository = null!;

    [SetUp]
    public void SetUp()
    {
        _roleRepository = new InMemoryRoleRepository();
        _userRepository = new InMemoryUserRepository();
        _service = new RoleService(_roleRepository, _userRepository);
    }

    [Test]
    public async Task GetRolesAsync_ReturnsRolesWithClaims()
    {
        var roleId = Guid.NewGuid();
        _roleRepository.Roles.Add(new Role { Id = roleId, Name = "Coach", Description = "Role" });
        _roleRepository.RoleClaims[roleId] =
        [
            AuthConstants.Permissions.ManageAppConfig,
            AuthConstants.Permissions.ManageUserRoles
        ];

        var roles = await _service.GetRolesAsync();

        Assert.That(roles, Has.Count.EqualTo(1));
        Assert.That(roles[0].Name, Is.EqualTo("Coach"));
        Assert.That(roles[0].PermissionClaims, Is.EquivalentTo(_roleRepository.RoleClaims[roleId]));
    }

    [Test]
    public void GetRoleAsync_ThrowsBadRequest_WhenRoleIdEmpty()
    {
        var exception = Assert.ThrowsAsync<AppException>(async () => await _service.GetRoleAsync(Guid.Empty));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.StatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CreateRoleAsync_NormalizesAndStoresClaims()
    {
        var result = await _service.CreateRoleAsync(
            "  Coach  ",
            "  Training tools  ",
            [
                AuthConstants.Permissions.ManageAppConfig,
                AuthConstants.Permissions.ManageUserRoles,
                AuthConstants.Permissions.ManageAppConfig
            ]);

        Assert.That(result.Name, Is.EqualTo("Coach"));
        Assert.That(result.Description, Is.EqualTo("Training tools"));
        Assert.That(result.PermissionClaims, Is.EqualTo(new[]
        {
            AuthConstants.Permissions.ManageAppConfig,
            AuthConstants.Permissions.ManageUserRoles
        }));
        Assert.That(_roleRepository.Roles.Any(r => r.Id == result.Id), Is.True);
    }

    [Test]
    public void CreateRoleAsync_ThrowsBadRequest_WhenClaimInvalid()
    {
        var exception = Assert.ThrowsAsync<AppException>(async () =>
            await _service.CreateRoleAsync("Coach", null, ["invalid.claim"]));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.StatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));
    }

    [Test]
    public void UpdateRoleAsync_ThrowsForbidden_ForSystemRole()
    {
        var adminRole = new Role { Id = Guid.NewGuid(), Name = AuthConstants.Roles.Admin };
        _roleRepository.Roles.Add(adminRole);

        var exception = Assert.ThrowsAsync<AppException>(async () =>
            await _service.UpdateRoleAsync(
                adminRole.Id,
                "AdminUpdated",
                null,
                [AuthConstants.Permissions.ManageUserRoles]));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.StatusCode, Is.EqualTo((int)HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task UpdateRoleAsync_UpdatesNameDescriptionAndClaims()
    {
        var roleId = Guid.NewGuid();
        _roleRepository.Roles.Add(new Role { Id = roleId, Name = "Coach", Description = "Old" });

        await _service.UpdateRoleAsync(
            roleId,
            "  Senior Coach  ",
            "  New desc  ",
            [AuthConstants.Permissions.ManageGlobalExercises]);

        var updated = _roleRepository.Roles.Single(r => r.Id == roleId);
        Assert.That(updated.Name, Is.EqualTo("Senior Coach"));
        Assert.That(updated.Description, Is.EqualTo("New desc"));
        Assert.That(_roleRepository.RoleClaims[roleId], Is.EqualTo(new[] { AuthConstants.Permissions.ManageGlobalExercises }));
    }

    [Test]
    public void DeleteRoleAsync_ThrowsForbidden_ForSystemRole()
    {
        var userRole = new Role { Id = Guid.NewGuid(), Name = AuthConstants.Roles.User };
        _roleRepository.Roles.Add(userRole);

        var exception = Assert.ThrowsAsync<AppException>(async () => await _service.DeleteRoleAsync(userRole.Id));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.StatusCode, Is.EqualTo((int)HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task DeleteRoleAsync_RemovesNonSystemRole()
    {
        var role = new Role { Id = Guid.NewGuid(), Name = "Coach" };
        _roleRepository.Roles.Add(role);

        await _service.DeleteRoleAsync(role.Id);

        Assert.That(_roleRepository.Roles.Any(r => r.Id == role.Id), Is.False);
    }

    [Test]
    public async Task UpdateUserRolesAsync_ReplacesUserRolesUsingDistinctNames()
    {
        var userId = Guid.NewGuid();
        _userRepository.Users.Add(new User { Id = userId, Name = "u", Email = "u@x.com" });

        var coach = new Role { Id = Guid.NewGuid(), Name = "Coach" };
        var analyst = new Role { Id = Guid.NewGuid(), Name = "Analyst" };
        _roleRepository.Roles.AddRange([coach, analyst]);

        await _service.UpdateUserRolesAsync(userId, [" coach ", "ANALYST", "coach"]);

        Assert.That(_roleRepository.UserRoleAssignments.TryGetValue(userId, out var roles), Is.True);
        Assert.That(roles!, Is.EquivalentTo(new[] { coach.Id, analyst.Id }));
    }

    [Test]
    public void UpdateUserRolesAsync_ThrowsNotFound_WhenUserMissing()
    {
        var exception = Assert.ThrowsAsync<AppException>(async () =>
            await _service.UpdateUserRolesAsync(Guid.NewGuid(), ["Coach"]));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.StatusCode, Is.EqualTo((int)HttpStatusCode.NotFound));
    }

    [Test]
    public void UpdateUserRolesAsync_ThrowsBadRequest_WhenRoleMissing()
    {
        var userId = Guid.NewGuid();
        _userRepository.Users.Add(new User { Id = userId, Name = "u", Email = "u@x.com" });

        var exception = Assert.ThrowsAsync<AppException>(async () =>
            await _service.UpdateUserRolesAsync(userId, ["UnknownRole"]));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.StatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));
    }

    [Test]
    public void GetAvailablePermissionClaims_ReturnsLocalizedCatalog()
    {
        var claims = _service.GetAvailablePermissionClaims();

        Assert.That(claims.Select(c => c.ClaimValue), Is.EqualTo(AuthConstants.Permissions.All.OrderBy(c => c, StringComparer.Ordinal).ToList()));
        Assert.That(claims.All(c => c.ClaimType == AuthConstants.PermissionClaimType), Is.True);
        Assert.That(claims.All(c => !string.IsNullOrWhiteSpace(c.DisplayName)), Is.True);
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        public List<User> Users { get; } = new();

        public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

        public Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.FirstOrDefault(u => u.Name == name));

        public Task<User?> FindByNameOrEmailAsync(string name, string email, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.FirstOrDefault(u => u.Name == name || u.Email == email));

        public Task<List<LgymApi.Application.Models.UserRankingEntry>> GetRankingAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<LgymApi.Application.Models.UserRankingEntry>());

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            Users.Add(user);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            var index = Users.FindIndex(u => u.Id == user.Id);
            if (index >= 0)
            {
                Users[index] = user;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryRoleRepository : IRoleRepository
    {
        public List<Role> Roles { get; } = new();
        public Dictionary<Guid, List<string>> RoleClaims { get; } = new();
        public Dictionary<Guid, List<Guid>> UserRoleAssignments { get; } = new();

        public Task<List<Role>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Roles.OrderBy(r => r.Name).ToList());

        public Task<Role?> FindByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
            => Task.FromResult(Roles.FirstOrDefault(r => r.Id == roleId));

        public Task<Role?> FindByNameAsync(string roleName, CancellationToken cancellationToken = default)
            => Task.FromResult(Roles.FirstOrDefault(r => string.Equals(r.Name, roleName, StringComparison.Ordinal)));

        public Task<List<Role>> GetByNamesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default)
        {
            var normalized = roleNames
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(Roles.Where(r => normalized.Contains(r.Name)).ToList());
        }

        public Task<bool> ExistsByNameAsync(string roleName, Guid? excludeRoleId = null, CancellationToken cancellationToken = default)
        {
            var exists = Roles.Any(r =>
                string.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase) &&
                (!excludeRoleId.HasValue || r.Id != excludeRoleId.Value));
            return Task.FromResult(exists);
        }

        public Task<List<string>> GetRoleNamesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (!UserRoleAssignments.TryGetValue(userId, out var roleIds))
            {
                return Task.FromResult(new List<string>());
            }

            return Task.FromResult(Roles.Where(r => roleIds.Contains(r.Id)).Select(r => r.Name).OrderBy(n => n).ToList());
        }

        public Task<List<string>> GetPermissionClaimsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (!UserRoleAssignments.TryGetValue(userId, out var roleIds))
            {
                return Task.FromResult(new List<string>());
            }

            var claims = roleIds
                .Where(RoleClaims.ContainsKey)
                .SelectMany(roleId => RoleClaims[roleId])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(c => c)
                .ToList();
            return Task.FromResult(claims);
        }

        public Task<List<string>> GetPermissionClaimsByRoleIdAsync(Guid roleId, CancellationToken cancellationToken = default)
            => Task.FromResult(RoleClaims.TryGetValue(roleId, out var claims)
                ? claims.OrderBy(c => c).ToList()
                : new List<string>());

        public Task<Dictionary<Guid, List<string>>> GetPermissionClaimsByRoleIdsAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default)
        {
            var result = roleIds
                .Distinct()
                .Where(roleId => RoleClaims.ContainsKey(roleId))
                .ToDictionary(
                    roleId => roleId,
                    roleId => RoleClaims[roleId].OrderBy(c => c).ToList());
            return Task.FromResult(result);
        }

        public Task<bool> UserHasRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
        {
            if (!UserRoleAssignments.TryGetValue(userId, out var roleIds))
            {
                return Task.FromResult(false);
            }

            var hasRole = Roles.Any(r => roleIds.Contains(r.Id) && string.Equals(r.Name, roleName, StringComparison.Ordinal));
            return Task.FromResult(hasRole);
        }

        public Task<bool> UserHasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default)
        {
            if (!UserRoleAssignments.TryGetValue(userId, out var roleIds))
            {
                return Task.FromResult(false);
            }

            var hasPermission = roleIds
                .Where(RoleClaims.ContainsKey)
                .SelectMany(roleId => RoleClaims[roleId])
                .Any(claim => string.Equals(claim, permission, StringComparison.Ordinal));

            return Task.FromResult(hasPermission);
        }

        public Task AddRoleAsync(Role role, CancellationToken cancellationToken = default)
        {
            Roles.Add(role);
            return Task.CompletedTask;
        }

        public Task UpdateRoleAsync(Role role, CancellationToken cancellationToken = default)
        {
            var index = Roles.FindIndex(r => r.Id == role.Id);
            if (index >= 0)
            {
                Roles[index] = role;
            }

            return Task.CompletedTask;
        }

        public Task DeleteRoleAsync(Role role, CancellationToken cancellationToken = default)
        {
            Roles.RemoveAll(r => r.Id == role.Id);
            RoleClaims.Remove(role.Id);
            foreach (var assignment in UserRoleAssignments.Values)
            {
                assignment.RemoveAll(roleId => roleId == role.Id);
            }

            return Task.CompletedTask;
        }

        public Task ReplaceRolePermissionClaimsAsync(Guid roleId, IReadOnlyCollection<string> permissionClaims, CancellationToken cancellationToken = default)
        {
            RoleClaims[roleId] = permissionClaims
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(c => c)
                .ToList();
            return Task.CompletedTask;
        }

        public Task AddUserRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default)
        {
            if (!UserRoleAssignments.TryGetValue(userId, out var assignments))
            {
                assignments = new List<Guid>();
                UserRoleAssignments[userId] = assignments;
            }

            foreach (var roleId in roleIds.Where(roleId => !assignments.Contains(roleId)))
            {
                assignments.Add(roleId);
            }

            return Task.CompletedTask;
        }

        public Task ReplaceUserRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default)
        {
            UserRoleAssignments[userId] = roleIds.Distinct().ToList();
            return Task.CompletedTask;
        }
    }
}
