using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Api.Configuration;

public static class StartupMigrationBootstrap
{
    public static async Task ApplyAsync(WebApplication app, string testingEnvironmentName)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(testingEnvironmentName);

        if (!ShouldApplyMigrations(app.Environment.EnvironmentName, testingEnvironmentName))
        {
            return;
        }

        await using var startupScope = app.Services.CreateAsyncScope();
        var dbContext = startupScope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (RequiresRuntimeValidation(app.Environment.EnvironmentName))
        {
            await PostgreSqlRuntimeConnectionValidator.ValidatePreMigrationAsync(dbContext, app.Configuration);
        }
        await dbContext.Database.MigrateAsync();
        if (RequiresRuntimeValidation(app.Environment.EnvironmentName))
        {
            await PostgreSqlRuntimeConnectionValidator.ValidateAsync(dbContext, app.Configuration);
        }
    }

    internal static bool ShouldApplyMigrations(string environmentName, string testingEnvironmentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(testingEnvironmentName);
        return !string.Equals(environmentName, testingEnvironmentName, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool RequiresRuntimeValidation(string environmentName)
        => !string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);
}
