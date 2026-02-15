using LgymApi.Infrastructure.Data;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public string DatabaseName { get; } = $"LgymTests_{Guid.NewGuid()}";

    public const string TestJwtSecret = "IntegrationTestSecretKey_MustBeAtLeast32Characters!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var descriptorsToRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(AppDbContext)
                         || d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(DatabaseName);
                options.EnableSensitiveDataLogging();
            });

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();

            if (!db.Roles.Any())
            {
                var timestamp = new DateTimeOffset(2026, 2, 15, 0, 0, 0, TimeSpan.Zero);
                db.Roles.AddRange(
                    new Role
                    {
                        Id = AppDbContext.UserRoleSeedId,
                        Name = AuthConstants.Roles.User,
                        Description = "Default role for all users",
                        CreatedAt = timestamp,
                        UpdatedAt = timestamp
                    },
                    new Role
                    {
                        Id = AppDbContext.AdminRoleSeedId,
                        Name = AuthConstants.Roles.Admin,
                        Description = "Administrative privileges",
                        CreatedAt = timestamp,
                        UpdatedAt = timestamp
                    },
                    new Role
                    {
                        Id = AppDbContext.TesterRoleSeedId,
                        Name = AuthConstants.Roles.Tester,
                        Description = "Excluded from ranking",
                        CreatedAt = timestamp,
                        UpdatedAt = timestamp
                    });
                db.RoleClaims.AddRange(
                    new RoleClaim
                    {
                        Id = AppDbContext.AdminAccessClaimSeedId,
                        RoleId = AppDbContext.AdminRoleSeedId,
                        ClaimType = AuthConstants.PermissionClaimType,
                        ClaimValue = AuthConstants.Permissions.AdminAccess,
                        CreatedAt = timestamp,
                        UpdatedAt = timestamp
                    },
                    new RoleClaim
                    {
                        Id = AppDbContext.ManageUserRolesClaimSeedId,
                        RoleId = AppDbContext.AdminRoleSeedId,
                        ClaimType = AuthConstants.PermissionClaimType,
                        ClaimValue = AuthConstants.Permissions.ManageUserRoles,
                        CreatedAt = timestamp,
                        UpdatedAt = timestamp
                    },
                    new RoleClaim
                    {
                        Id = AppDbContext.ManageAppConfigClaimSeedId,
                        RoleId = AppDbContext.AdminRoleSeedId,
                        ClaimType = AuthConstants.PermissionClaimType,
                        ClaimValue = AuthConstants.Permissions.ManageAppConfig,
                        CreatedAt = timestamp,
                        UpdatedAt = timestamp
                    },
                    new RoleClaim
                    {
                        Id = AppDbContext.ManageGlobalExercisesClaimSeedId,
                        RoleId = AppDbContext.AdminRoleSeedId,
                        ClaimType = AuthConstants.PermissionClaimType,
                        ClaimValue = AuthConstants.Permissions.ManageGlobalExercises,
                        CreatedAt = timestamp,
                        UpdatedAt = timestamp
                    });
                db.SaveChanges();
            }
        });

        builder.UseSetting("Jwt:Secret", TestJwtSecret);
    }
}
