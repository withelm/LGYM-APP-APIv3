using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Pagination;
using LgymApi.Infrastructure.Repositories;
using LgymApi.Application.Pagination;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class RoleRepositoryTests
{
    [Test]
    public async Task ExistsByNameAsync_IsCaseInsensitiveAndSupportsExcludeRole()
    {
        await using var db = CreateDbContext("role-repo-exists");
        var adminRole = new Role { Id = Id<Role>.New(), Name = "Admin" };
        db.Roles.Add(adminRole);
        await db.SaveChangesAsync();

        var repository = CreateRoleRepository(db);

        var exists = await repository.ExistsByNameAsync(" admin ");
        var existsExcludingSame = await repository.ExistsByNameAsync("ADMIN", adminRole.Id);

        exists.Should().BeTrue();
        existsExcludingSame.Should().BeFalse();
    }

    [Test]
    public async Task GetByNamesAsync_WhenEmptyInput_ReturnsEmptyList()
    {
        await using var db = CreateDbContext("role-repo-empty");
        var repository = CreateRoleRepository(db);

        var roles = await repository.GetByNamesAsync([]);

        roles.Should().BeEmpty();
    }

    [Test]
    public async Task AddUserRolesAsync_DoesNotDuplicateExistingRoles()
    {
        await using var db = CreateDbContext("role-repo-user-roles");
        var userId = Id<User>.New();
        var existingRole = new Role { Id = Id<Role>.New(), Name = "Trainer" };
        var missingRole = new Role { Id = Id<Role>.New(), Name = "Tester" };

        db.Roles.AddRange(existingRole, missingRole);
        db.Users.Add(new User { Id = userId, Name = "user", Email = "user-role-repo@example.com", ProfileRank = "Rookie" });
        db.UserRoles.Add(new UserRole { UserId = userId, RoleId = existingRole.Id });
        await db.SaveChangesAsync();

        var repository = CreateRoleRepository(db);

        await repository.AddUserRolesAsync(userId, [existingRole.Id, missingRole.Id]);
        await db.SaveChangesAsync();

        var roleIds = await db.UserRoles.Where(x => x.UserId == userId).Select(x => x.RoleId).ToListAsync();
        roleIds.Should().BeEquivalentTo([existingRole.Id, missingRole.Id]);
    }

    [Test]
    public async Task ReplaceRolePermissionClaimsAsync_TrimsDistinctsAndReplacesClaims()
    {
        await using var db = CreateDbContext("role-repo-claims");
        var roleId = Id<Role>.New();
        db.Roles.Add(new Role { Id = roleId, Name = "Admin" });
        db.RoleClaims.Add(new RoleClaim
        {
            Id = Id<RoleClaim>.New(),
            RoleId = roleId,
            ClaimType = AuthConstants.PermissionClaimType,
            ClaimValue = "old.permission"
        });
        await db.SaveChangesAsync();

        var repository = CreateRoleRepository(db);
        await repository.ReplaceRolePermissionClaimsAsync(roleId, [" users.roles.manage ", "users.roles.manage", "app.config.manage", " "]);
        await db.SaveChangesAsync();

        var claims = await db.RoleClaims
            .Where(x => x.RoleId == roleId && x.ClaimType == AuthConstants.PermissionClaimType)
            .OrderBy(x => x.ClaimValue)
            .Select(x => x.ClaimValue)
            .ToListAsync();

        claims.Should().Equal("app.config.manage", "users.roles.manage");
    }

    private static RoleRepository CreateRoleRepository(AppDbContext db) =>
        new(db, null!, new MapperRegistry());

    private static AppDbContext CreateDbContext(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"{name}-{Id<RoleRepositoryTests>.New():N}")
            .Options;

        return new AppDbContext(options);
    }
}
