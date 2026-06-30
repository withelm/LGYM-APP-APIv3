using FluentAssertions;
using LgymApi.Api.Configuration;
using LgymApi.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
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

        await action.Should().ThrowAsync<InvalidOperationException>();
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

        provider.Messages.Should().ContainSingle(message => message.Contains("CloudflareR2") && message.Contains("bucket") && message.Contains("https://endpoint"));
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

        result.Should().BeTrue();
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

        result.Should().BeFalse();
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
