using FluentAssertions;
using LgymApi.Api.Configuration;
using LgymApi.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("PostgreSql")]
public sealed class StartupMigrationBootstrapPostgreSqlTests
{
    private const string PrePolicyMigrationId = "20260801222135_RepairRecurringReportAssignmentRequestIndex";

    [Test]
    public async Task ApplyAsync_InStaging_AppliesPendingMigrationWithRuntimeRole()
    {
        await using var environment = await PostgreSqlTutorialRowSecurityTestEnvironment.CreateAsync(activate: false);
        await using (var database = CreateDbContext(environment.RuntimeConnectionString))
        {
            await database.Database.GetService<IMigrator>().MigrateAsync(PrePolicyMigrationId);
        }

        await using var app = CreateApp(environment);

        var action = () => StartupMigrationBootstrap.ApplyAsync(app, "Testing");

        await action.Should().NotThrowAsync();
        await using var verification = CreateDbContext(environment.RuntimeConnectionString);
        (await verification.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }

    private static WebApplication CreateApp(PostgreSqlTutorialRowSecurityTestEnvironment environment)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Staging" });
        builder.Configuration
            .SetBasePath(FindRepositoryRoot())
            .AddJsonFile("appsettings.container.example.json", optional: false)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PostgreSqlRuntime:ExpectedDatabase"] = environment.DatabaseName,
                ["PostgreSqlRuntime:ExpectedRole"] = environment.RuntimeRole,
                ["PostgreSqlRuntime:HangfireSchema"] = "hangfire"
            });
        builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(environment.RuntimeConnectionString));
        return builder.Build();
    }

    private static AppDbContext CreateDbContext(string connectionString)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LgymApi.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
