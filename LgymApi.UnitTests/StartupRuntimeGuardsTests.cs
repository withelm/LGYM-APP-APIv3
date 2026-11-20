using LgymApi.Api.Configuration;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Data;
using System.Data.Common;
using System.Reflection;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class StartupRuntimeGuardsTests
{
    [Test]
    public async Task ValidateDatabaseSchemaAsync_WhenMigrationsPending_ThrowsInvalidOperationException()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();

        var action = async () => await StartupRuntimeGuards.ValidateDatabaseSchemaAsync(provider);

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void LogPhotoStorageConfiguration_LogsConfiguredValues()
    {
        var provider = new CapturingLoggerProvider();
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PhotoStorage:Provider"] = "CloudflareR2",
            ["PhotoStorage:BucketName"] = "bucket",
            ["PhotoStorage:Endpoint"] = "https://endpoint"
        });
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(provider);
        var app = builder.Build();

        app.LogPhotoStorageConfiguration();

        Assert.That((provider.Messages).Where(message => message.Contains("CloudflareR2") && message.Contains("bucket") && message.Contains("https://endpoint")), Has.Exactly(1).Items);
    }

    [Test]
    public async Task TableExistsAsync_WhenSqliteTableExists_ReturnsTrue()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.ExecuteSqlRawAsync("CREATE TABLE ExistingTable (Id INTEGER PRIMARY KEY);");

        var result = await InvokeTableExistsAsync(dbContext, null, "ExistingTable");

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task TableExistsAsync_WhenSqliteTableMissing_ReturnsFalse()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new AppDbContext(options);

        var result = await InvokeTableExistsAsync(dbContext, null, "MissingTable");

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task ValidateDatabaseSchemaAsync_WhenUsingNonRelationalProvider_PropagatesRelationalProviderError()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase($"runtime-guards-{Id<StartupRuntimeGuardsTests>.New()}"));
        await using var provider = services.BuildServiceProvider();

        var action = async () => await StartupRuntimeGuards.ValidateDatabaseSchemaAsync(provider);

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void TableIdentifier_ToDelimitedSqlAndDisplayName_ReturnExpectedShapes()
    {
        var tableIdentifierType = typeof(StartupRuntimeGuards).GetNestedType("TableIdentifier", BindingFlags.NonPublic)!;
        var withSchema = Activator.CreateInstance(tableIdentifierType, "public", "Report\"Requests")!;
        var withoutSchema = Activator.CreateInstance(tableIdentifierType, null, "Photos")!;
        var toDelimitedSql = tableIdentifierType.GetMethod("ToDelimitedSql", BindingFlags.Instance | BindingFlags.Public)!;
        var toDisplayName = tableIdentifierType.GetMethod("ToDisplayName", BindingFlags.Instance | BindingFlags.Public)!;

        Assert.That(toDelimitedSql.Invoke(withSchema, []), Is.EqualTo("\"public\".\"Report\"\"Requests\""));
        Assert.That(toDisplayName.Invoke(withSchema, []), Is.EqualTo("public.Report\"Requests"));
        Assert.That(toDelimitedSql.Invoke(withoutSchema, []), Is.EqualTo("\"Photos\""));
        Assert.That(toDisplayName.Invoke(withoutSchema, []), Is.EqualTo("Photos"));
    }

    [Test]
    public async Task ValidateMappedTablesExistAsync_WhenMappedTablesMissing_ThrowsDetailedError()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new AppDbContext(options);

        var action = async () => await InvokeValidateMappedTablesExistAsync(dbContext);

        var exception = (await Assert.CatchAsync<InvalidOperationException>(async () => { await action(); }))!;
        Assert.That(exception.Message, Does.Contain("mapped tables are missing"));
    }

    [Test]
    public async Task ExecuteExistsScalarAsync_WhenConnectionInitiallyClosed_OpensAndClosesConnectionAroundQuery()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new AppDbContext(options);
        Assert.That(connection.State, Is.EqualTo(System.Data.ConnectionState.Closed));

        var result = await InvokeExecuteExistsScalarAsync(dbContext, "SELECT 1;");

        Assert.That(result, Is.True);
        Assert.That(connection.State, Is.EqualTo(System.Data.ConnectionState.Closed));
    }

    [Test]
    public async Task InformationSchemaTableExistsAsync_WhenProviderQueryFails_BubblesExceptionAfterCommandSetup()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new AppDbContext(options);
        var tableIdentifierType = typeof(StartupRuntimeGuards).GetNestedType("TableIdentifier", BindingFlags.NonPublic)!;
        var tableIdentifier = Activator.CreateInstance(tableIdentifierType, "public", "Anything")!;
        var method = typeof(StartupRuntimeGuards).GetMethod("InformationSchemaTableExistsAsync", BindingFlags.NonPublic | BindingFlags.Static)!;

        var action = async () => await (Task<bool>)method.Invoke(null, [dbContext, tableIdentifier])!;

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<SqliteException>());
    }

    [Test]
    public void CreateParameter_WhenExplicitDbTypeProvided_AppliesTypeEvenForNullValue()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var command = connection.CreateCommand();
        var parameterSpecType = typeof(StartupRuntimeGuards).GetNestedType("SqlParameterSpec", BindingFlags.NonPublic)!;
        var parameterSpec = Activator.CreateInstance(parameterSpecType, "@tableSchema", null, DbType.String)!;
        var method = typeof(StartupRuntimeGuards).GetMethod("CreateParameter", BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = (DbParameter)method.Invoke(null, [command, parameterSpec])!;

        Assert.That(result.ParameterName, Is.EqualTo("@tableSchema"));
        Assert.That(result.Value, Is.EqualTo(DBNull.Value));
        Assert.That(result.DbType, Is.EqualTo(DbType.String));
    }

    private static async Task InvokeValidateMappedTablesExistAsync(AppDbContext dbContext)
    {
        var method = typeof(StartupRuntimeGuards).GetMethod("ValidateMappedTablesExistAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var task = (Task)method.Invoke(null, [dbContext])!;
        await task;
    }

    private static async Task<bool> InvokeExecuteExistsScalarAsync(AppDbContext dbContext, string sql)
    {
        var method = typeof(StartupRuntimeGuards).GetMethod(
            "ExecuteExistsScalarAsync",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            [typeof(AppDbContext), typeof(string), typeof((string Name, object? Value)[])],
            modifiers: null)!;
        var task = (Task<bool>)method.Invoke(null, [dbContext, sql, Array.Empty<(string Name, object? Value)>()])!;
        return await task;
    }

    private static async Task<bool> InvokeTableExistsAsync(AppDbContext dbContext, string? schema, string tableName)
    {
        var tableIdentifierType = typeof(StartupRuntimeGuards).GetNestedType("TableIdentifier", BindingFlags.NonPublic)!;
        var tableIdentifier = Activator.CreateInstance(tableIdentifierType, schema, tableName)!;
        var method = typeof(StartupRuntimeGuards).GetMethod("TableExistsAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var task = (Task<bool>)method.Invoke(null, [dbContext, tableIdentifier])!;
        return await task;
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(List<string> messages) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            messages.Add(formatter(state, exception));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
