using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class AppDbContextFactoryTests
{
    private string? _originalMigrationConnection;
    private string? _originalRuntimeConnection;
    private string? _originalEnvironment;

    [SetUp]
    public void SetUp()
    {
        _originalMigrationConnection = Environment.GetEnvironmentVariable("LGYM_MIGRATION_POSTGRES");
        _originalRuntimeConnection = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        _originalEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("LGYM_MIGRATION_POSTGRES", _originalMigrationConnection);
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", _originalRuntimeConnection);
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", _originalEnvironment);
    }

    [Test]
    public void CreateDbContext_WhenDevelopmentConnectionVariablesAreMissing_UsesLocalFallback()
    {
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("LGYM_MIGRATION_POSTGRES", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", null);
        var factory = new AppDbContextFactory();

        using var context = factory.CreateDbContext([]);

        Assert.That(context.Database.GetDbConnection().Database, Is.EqualTo("LGYM-APP"));
        Assert.That(context.Database.GetDbConnection().DataSource, Does.Contain("localhost"));
    }

    [Test]
    public void CreateDbContext_WhenMigrationEnvironmentVariableProvided_PrefersMaintenanceConnection()
    {
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Production");
        Environment.SetEnvironmentVariable("LGYM_MIGRATION_POSTGRES", "Host=maintenance;Port=5432;Database=design_time;Username=maintenance;Password=test-only");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "Host=runtime;Port=5432;Database=runtime;Username=runtime;Password=test-only");
        var factory = new AppDbContextFactory();

        using var context = factory.CreateDbContext([]);

        Assert.That(context.Database.GetDbConnection().Database, Is.EqualTo("design_time"));
        Assert.That(context.Database.GetDbConnection().DataSource, Does.Contain("maintenance"));
    }

    [Test]
    public void CreateDbContext_OutsideDevelopmentWithoutMaintenanceConnection_Throws()
    {
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Production");
        Environment.SetEnvironmentVariable("LGYM_MIGRATION_POSTGRES", null);

        var action = () => new AppDbContextFactory().CreateDbContext([]);

        Assert.That(() => { action(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("*LGYM_MIGRATION_POSTGRES*").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void CreateDbContext_BuildsCanonicalNpgsqlModelAndMigrationStream()
    {
        const string connectionString = "Host=127.0.0.1;Port=1;Database=design_time_guard;Username=guard;Password=guard";
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Production");
        Environment.SetEnvironmentVariable("LGYM_MIGRATION_POSTGRES", connectionString);
        var factory = new AppDbContextFactory();

        using var context = factory.CreateDbContext([]);
        var migrationsAssembly = context.GetService<IMigrationsAssembly>();

        Assert.That(context.Database.ProviderName, Is.EqualTo("Npgsql.EntityFrameworkCore.PostgreSQL"));
        Assert.That(context.Model.GetEntityTypes(), Has.Exactly(55).Items);
        Assert.That(context.Database.HasPendingModelChanges(), Is.False);
        Assert.That(migrationsAssembly.Assembly, Is.SameAs(typeof(AppDbContext).Assembly));
        Assert.That(migrationsAssembly.ModelSnapshot, Is.Not.Null);
        Assert.That(migrationsAssembly.ModelSnapshot!.GetType().Name, Is.EqualTo("AppDbContextModelSnapshot"));
    }
}
