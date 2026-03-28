using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Seeders;

public sealed class RoleClaimSeeder : IEntitySeeder
{
    public int Order => 3;

    public async Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("role claims");
        if (seedContext.RoleClaims.Count > 0)
        {
            SeedOperationConsole.Skip("role claims");
            return;
        }

        var existing = await context.RoleClaims
            .AsNoTracking()
            .Select(claim => new { claim.RoleId, claim.ClaimType, claim.ClaimValue })
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<(Id<Role> RoleId, string ClaimType, string ClaimValue)>(
            existing.Select(entry => (entry.RoleId, entry.ClaimType, entry.ClaimValue)));

        var claims = new List<RoleClaim>
        {
            new()
            {
                Id = (LgymApi.Domain.ValueObjects.Id<RoleClaim>)AppDbContext.AdminAccessClaimSeedId,
                RoleId = (LgymApi.Domain.ValueObjects.Id<Role>)AppDbContext.AdminRoleSeedId,
                ClaimType = AuthConstants.PermissionClaimType,
                ClaimValue = AuthConstants.Permissions.AdminAccess
            },
            new()
            {
                Id = (LgymApi.Domain.ValueObjects.Id<RoleClaim>)AppDbContext.ManageUserRolesClaimSeedId,
                RoleId = (LgymApi.Domain.ValueObjects.Id<Role>)AppDbContext.AdminRoleSeedId,
                ClaimType = AuthConstants.PermissionClaimType,
                ClaimValue = AuthConstants.Permissions.ManageUserRoles
            },
            new()
            {
                Id = (LgymApi.Domain.ValueObjects.Id<RoleClaim>)AppDbContext.ManageAppConfigClaimSeedId,
                RoleId = (LgymApi.Domain.ValueObjects.Id<Role>)AppDbContext.AdminRoleSeedId,
                ClaimType = AuthConstants.PermissionClaimType,
                ClaimValue = AuthConstants.Permissions.ManageAppConfig
            },
            new()
            {
                Id = (LgymApi.Domain.ValueObjects.Id<RoleClaim>)AppDbContext.ManageGlobalExercisesClaimSeedId,
                RoleId = (LgymApi.Domain.ValueObjects.Id<Role>)AppDbContext.AdminRoleSeedId,
                ClaimType = AuthConstants.PermissionClaimType,
                ClaimValue = AuthConstants.Permissions.ManageGlobalExercises
            }
        };

        var addedAny = false;
        foreach (var claim in claims)
        {
            if (!existingSet.Add((claim.RoleId, claim.ClaimType, claim.ClaimValue)))
            {
                continue;
            }

            await context.RoleClaims.AddAsync(claim, cancellationToken);
            seedContext.RoleClaims.Add(claim);
            addedAny = true;
        }

        if (!addedAny)
        {
            SeedOperationConsole.Skip("role claims");
            return;
        }

        SeedOperationConsole.Done("role claims");
    }
}
