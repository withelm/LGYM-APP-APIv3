using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
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

        var existingSet = new HashSet<(Guid RoleId, string ClaimType, string ClaimValue)>(
            existing.Select(entry => (entry.RoleId, entry.ClaimType, entry.ClaimValue)));

        var claims = new List<RoleClaim>
        {
            new()
            {
                Id = AppDbContext.AdminAccessClaimSeedId,
                RoleId = AppDbContext.AdminRoleSeedId,
                ClaimType = AuthConstants.PermissionClaimType,
                ClaimValue = AuthConstants.Permissions.AdminAccess
            },
            new()
            {
                Id = AppDbContext.ManageUserRolesClaimSeedId,
                RoleId = AppDbContext.AdminRoleSeedId,
                ClaimType = AuthConstants.PermissionClaimType,
                ClaimValue = AuthConstants.Permissions.ManageUserRoles
            },
            new()
            {
                Id = AppDbContext.ManageAppConfigClaimSeedId,
                RoleId = AppDbContext.AdminRoleSeedId,
                ClaimType = AuthConstants.PermissionClaimType,
                ClaimValue = AuthConstants.Permissions.ManageAppConfig
            },
            new()
            {
                Id = AppDbContext.ManageGlobalExercisesClaimSeedId,
                RoleId = AppDbContext.AdminRoleSeedId,
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
