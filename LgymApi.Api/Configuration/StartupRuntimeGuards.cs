using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.Api.Configuration;

internal static class StartupRuntimeGuards
{
    public static async Task ValidateDatabaseSchemaAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        var pendingMigrationList = pendingMigrations.ToList();

        if (pendingMigrationList.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Database schema is behind the application model. Apply pending EF Core migrations before starting the API. " +
            $"Pending migrations: {string.Join(", ", pendingMigrationList)}");
    }

    public static void LogPhotoStorageConfiguration(this WebApplication app)
    {
        var photoStorageProvider = app.Configuration["PhotoStorage:Provider"] ?? "Local";
        var photoStorageBucket = app.Configuration["PhotoStorage:BucketName"] ?? string.Empty;
        var photoStorageEndpoint = app.Configuration["PhotoStorage:Endpoint"] ?? string.Empty;

        app.Logger.LogInformation(
            "Photo storage provider configured: {Provider}, bucket: {BucketName}, endpoint: {Endpoint}",
            photoStorageProvider,
            photoStorageBucket,
            photoStorageEndpoint);
    }
}
