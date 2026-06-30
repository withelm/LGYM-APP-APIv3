using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

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
            await ValidateMappedTablesExistAsync(dbContext);
            return;
        }

        throw new InvalidOperationException(
            "Database schema is behind the application model. Apply pending EF Core migrations before starting the API. " +
            $"Pending migrations: {string.Join(", ", pendingMigrationList)}");
    }

    private static async Task ValidateMappedTablesExistAsync(AppDbContext dbContext)
    {
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        var mappedTables = dbContext.Model
            .GetEntityTypes()
            .Where(entityType => !entityType.IsOwned() && entityType.GetTableName() is not null)
            .Select(entityType => new TableIdentifier(entityType.GetSchema(), entityType.GetTableName()!))
            .Distinct()
            .OrderBy(table => table.Schema)
            .ThenBy(table => table.Name)
            .ToList();

        var missingTables = new List<string>();

        foreach (var table in mappedTables)
        {
            try
            {
                var validationSql = $"SELECT 1 FROM {table.ToDelimitedSql()} LIMIT 1;";
                await dbContext.Database.ExecuteSqlRawAsync(validationSql);
            }
            catch (Exception ex) when (IsMissingTableException(ex))
            {
                missingTables.Add(table.ToDisplayName());
            }
        }

        if (missingTables.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Database schema is inconsistent with the application model. The following mapped tables are missing: " +
            $"{string.Join(", ", missingTables)}. " +
            "This usually means migrations history drift, manual schema changes, or a database created outside the EF Core migrations flow.");
    }

    private static bool IsMissingTableException(Exception exception)
    {
        var message = exception.ToString();
        return message.Contains("42P01", StringComparison.OrdinalIgnoreCase)
               || message.Contains("relation", StringComparison.OrdinalIgnoreCase) && message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
               || message.Contains("no such table", StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct TableIdentifier(string? Schema, string Name)
    {
        public string ToDelimitedSql()
            => Schema is null
                ? $"\"{Escape(Name)}\""
                : $"\"{Escape(Schema)}\".\"{Escape(Name)}\"";

        public string ToDisplayName()
            => Schema is null ? Name : $"{Schema}.{Name}";

        private static string Escape(string identifier)
            => identifier.Replace("\"", "\"\"");
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
