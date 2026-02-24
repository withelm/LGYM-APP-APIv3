using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Seeders;

public sealed class AppConfigSeeder : IEntitySeeder
{
    public int Order => 60;

    public async Task SeedAsync(AppDbContext context, SeedContext seedContext, CancellationToken cancellationToken)
    {
        SeedOperationConsole.Start("app configs");
        if (seedContext.AppConfigs.Count > 0)
        {
            SeedOperationConsole.Skip("app configs");
            return;
        }

        var existing = await context.AppConfigs
            .AsNoTracking()
            .Select(config => config.Platform)
            .ToListAsync(cancellationToken);

        var configs = new List<AppConfig>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Platform = Platforms.Android,
                MinRequiredVersion = "1.0.0",
                LatestVersion = "1.0.0",
                ForceUpdate = false,
                UpdateUrl = "https://play.google.com/store/apps/details?id=lgym",
                ReleaseNotes = "Initial release"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Platform = Platforms.Ios,
                MinRequiredVersion = "1.0.0",
                LatestVersion = "1.0.0",
                ForceUpdate = false,
                UpdateUrl = "https://apps.apple.com/app/lgym",
                ReleaseNotes = "Initial release"
            }
        };

        var addedAny = false;
        foreach (var config in configs)
        {
            if (existing.Contains(config.Platform))
            {
                continue;
            }

            await context.AppConfigs.AddAsync(config, cancellationToken);
            seedContext.AppConfigs.Add(config);
            addedAny = true;
        }

        if (!addedAny)
        {
            SeedOperationConsole.Skip("app configs");
            return;
        }

        SeedOperationConsole.Done("app configs");
    }
}
