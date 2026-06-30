using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Data;

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
            if (!await TableExistsAsync(dbContext, table))
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

    private static async Task<bool> TableExistsAsync(AppDbContext dbContext, TableIdentifier table)
    {
        var providerName = dbContext.Database.ProviderName;
        return providerName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true
            ? await SqliteTableExistsAsync(dbContext, table)
            : await InformationSchemaTableExistsAsync(dbContext, table);
    }

    private static async Task<bool> SqliteTableExistsAsync(AppDbContext dbContext, TableIdentifier table)
        => await ExecuteExistsScalarAsync(
            dbContext,
            "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @tableName LIMIT 1;",
            ("@tableName", table.Name));

    private static async Task<bool> InformationSchemaTableExistsAsync(AppDbContext dbContext, TableIdentifier table)
        => await ExecuteExistsScalarAsync(
            dbContext,
            "SELECT 1 FROM information_schema.tables WHERE table_name = @tableName AND ((@tableSchema IS NULL AND table_schema = current_schema()) OR (@tableSchema IS NOT NULL AND table_schema = @tableSchema)) LIMIT 1;",
            ("@tableName", table.Name),
            ("@tableSchema", table.Schema));

    private static async Task<bool> ExecuteExistsScalarAsync(AppDbContext dbContext, string sql, params (string Name, object? Value)[] parameters)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            foreach (var parameter in parameters)
            {
                var dbParameter = command.CreateParameter();
                dbParameter.ParameterName = parameter.Name;
                dbParameter.Value = parameter.Value ?? DBNull.Value;
                command.Parameters.Add(dbParameter);
            }

            var scalar = await command.ExecuteScalarAsync();
            return scalar is not null && scalar != DBNull.Value;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
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
