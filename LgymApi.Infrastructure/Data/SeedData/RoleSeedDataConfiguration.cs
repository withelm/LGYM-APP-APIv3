using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Infrastructure.Data.SeedData;

internal static class RoleSeedDataConfiguration
{
    public static readonly Id<Role> UserRoleSeedId = ParseSeedId<Role>("f124fe5f-9bf2-45df-bfd2-d5d6be920016");
    public static readonly Id<Role> AdminRoleSeedId = ParseSeedId<Role>("1754c6f8-c021-41aa-b610-17088f9476f9");
    public static readonly Id<Role> TesterRoleSeedId = ParseSeedId<Role>("f93f03af-ae11-4fd8-a60e-f970f89df6fb");
    public static readonly Id<Role> TrainerRoleSeedId = ParseSeedId<Role>("8c1a3db8-72a3-47cc-b3de-f5347c6ae501");
    public static readonly Id<RoleClaim> AdminAccessClaimSeedId = ParseSeedId<RoleClaim>("9dbfd057-cf88-4597-b668-2fdf16a2def6");
    public static readonly Id<RoleClaim> ManageUserRolesClaimSeedId = ParseSeedId<RoleClaim>("97f7ea56-0032-4f18-8703-ab2d1485ad45");
    public static readonly Id<RoleClaim> ManageAppConfigClaimSeedId = ParseSeedId<RoleClaim>("d12f9f84-48f4-4f4b-9614-843f31ea0f96");
    public static readonly Id<RoleClaim> ManageGlobalExercisesClaimSeedId = ParseSeedId<RoleClaim>("27965bf4-ff55-4261-8f98-218ccf00e537");
    public static readonly Id<RoleClaim> TrainerAccessClaimSeedId = ParseSeedId<RoleClaim>("a3b7c9d1-4e5f-6a7b-8c9d-0e1f2a3b4c5d");
    private static readonly DateTimeOffset RoleSeedTimestamp = new(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);

    public static void Apply(ModelBuilder modelBuilder)
    {
        var roleEntity = modelBuilder.Entity<Role>();
        roleEntity.HasData(
            new Role
            {
                Id = (Id<Role>)UserRoleSeedId,
                Name = AuthConstants.Roles.User,
                Description = "Default role for all users",
                CreatedAt = RoleSeedTimestamp,
                UpdatedAt = RoleSeedTimestamp
            },
            new Role
            {
                Id = (Id<Role>)AdminRoleSeedId,
                Name = AuthConstants.Roles.Admin,
                Description = "Administrative privileges",
                CreatedAt = RoleSeedTimestamp,
                UpdatedAt = RoleSeedTimestamp
            },
            new Role
            {
                Id = (Id<Role>)TesterRoleSeedId,
                Name = AuthConstants.Roles.Tester,
                Description = "Excluded from ranking",
                CreatedAt = RoleSeedTimestamp,
                UpdatedAt = RoleSeedTimestamp
            },
            new Role
            {
                Id = (Id<Role>)TrainerRoleSeedId,
                Name = AuthConstants.Roles.Trainer,
                Description = "Trainer role for coach-facing APIs",
                CreatedAt = RoleSeedTimestamp,
                UpdatedAt = RoleSeedTimestamp
            });

        var roleClaimEntity = modelBuilder.Entity<RoleClaim>();
        roleClaimEntity.HasData(
            new RoleClaim
            {
                Id = (Id<RoleClaim>)AdminAccessClaimSeedId,
                RoleId = (Id<Role>)AdminRoleSeedId,
                ClaimType = AuthConstants.PermissionClaimType,
                ClaimValue = AuthConstants.Permissions.AdminAccess,
                CreatedAt = RoleSeedTimestamp,
                UpdatedAt = RoleSeedTimestamp
            },
            new RoleClaim
            {
                Id = (Id<RoleClaim>)ManageUserRolesClaimSeedId,
                RoleId = (Id<Role>)AdminRoleSeedId,
                ClaimType = AuthConstants.PermissionClaimType,
                ClaimValue = AuthConstants.Permissions.ManageUserRoles,
                CreatedAt = RoleSeedTimestamp,
                UpdatedAt = RoleSeedTimestamp
            },
            new RoleClaim
            {
                Id = (Id<RoleClaim>)ManageAppConfigClaimSeedId,
                RoleId = (Id<Role>)AdminRoleSeedId,
                ClaimType = AuthConstants.PermissionClaimType,
                ClaimValue = AuthConstants.Permissions.ManageAppConfig,
                CreatedAt = RoleSeedTimestamp,
                UpdatedAt = RoleSeedTimestamp
            },
            new RoleClaim
            {
                Id = (Id<RoleClaim>)ManageGlobalExercisesClaimSeedId,
                RoleId = (Id<Role>)AdminRoleSeedId,
                ClaimType = AuthConstants.PermissionClaimType,
                ClaimValue = AuthConstants.Permissions.ManageGlobalExercises,
                CreatedAt = RoleSeedTimestamp,
                UpdatedAt = RoleSeedTimestamp
            },
            new RoleClaim
            {
                Id = (Id<RoleClaim>)TrainerAccessClaimSeedId,
                RoleId = (Id<Role>)TrainerRoleSeedId,
                ClaimType = AuthConstants.PermissionClaimType,
                ClaimValue = AuthConstants.Permissions.TrainerAccess,
                CreatedAt = RoleSeedTimestamp,
                UpdatedAt = RoleSeedTimestamp
            });
    }

    private static Id<TEntity> ParseSeedId<TEntity>(string idString)
    {
        if (!Id<TEntity>.TryParse(idString, out var id))
        {
            throw new InvalidOperationException($"Failed to parse seed ID: {idString}");
        }
        return id;
    }
}
