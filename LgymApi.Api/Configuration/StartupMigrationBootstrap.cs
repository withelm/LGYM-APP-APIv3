using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Api.Configuration;

public static class StartupMigrationBootstrap
{
    public static async Task ApplyAsync(WebApplication app, string testingEnvironmentName)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(testingEnvironmentName);

        if (app.Environment.IsEnvironment(testingEnvironmentName))
        {
            return;
        }

        await using var startupScope = app.Services.CreateAsyncScope();
        var dbContext = startupScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
