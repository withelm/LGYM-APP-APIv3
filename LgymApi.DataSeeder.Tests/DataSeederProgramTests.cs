using LgymApi.Application.Services;
using LgymApi.DataSeeder.Seeders;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace LgymApi.DataSeeder.Tests;

[TestFixture]
public sealed class DataSeederProgramTests
{
    [Test]
    public void GetMigrationConnectionString_ReadsDedicatedEnvironmentVariable()
    {
        var original = Environment.GetEnvironmentVariable("LGYM_MIGRATION_POSTGRES");
        try
        {
            Environment.SetEnvironmentVariable("LGYM_MIGRATION_POSTGRES", "Host=maintenance;Database=seeder;Username=maintenance;Password=test-only");

            Assert.That(string.IsNullOrWhiteSpace(DataSeederProgram.GetMigrationConnectionString()), Is.False);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LGYM_MIGRATION_POSTGRES", original);
        }
    }

    [Test]
    public void GetMigrationConnectionString_ReturnsNull_WhenEnvironmentVariableIsMissing()
    {
        var original = Environment.GetEnvironmentVariable("LGYM_MIGRATION_POSTGRES");
        try
        {
            Environment.SetEnvironmentVariable("LGYM_MIGRATION_POSTGRES", null);

            Assert.That(DataSeederProgram.GetMigrationConnectionString(), Is.Null);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LGYM_MIGRATION_POSTGRES", original);
        }
    }

    [Test]
    public void BuildConfiguration_Should_Load_Base_And_Optional_Appsettings()
    {
        var repoRoot = CreateTempRepo();
        try
        {
            var config = DataSeederProgram.BuildConfiguration(repoRoot);

            Assert.That(config.GetConnectionString("Postgres"), Is.EqualTo("Host=localhost"));
            Assert.That(config["FeatureFlag"], Is.EqualTo("true"));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Test]
    public void BuildServiceProvider_Should_Resolve_Identity_Password_Service_And_All_Seeders()
    {
        var configuration = new ConfigurationBuilder().Build();

        using var provider = BuildServiceProvider(configuration);
        using var scope = provider.CreateScope();

        var passwordService = scope.ServiceProvider.GetRequiredService<ILegacyPasswordService>();
        var password = passwordService.Create("seed-password");
        var seeders = scope.ServiceProvider.GetRequiredService<IEnumerable<IEntitySeeder>>().ToList();

        Assert.That(passwordService.Verify("seed-password", password.Hash, password.Salt, password.Iterations, password.KeyLength, password.Digest), Is.True);
        Assert.That(seeders, Has.Exactly(42).Items);
        Assert.That((seeders).Where(seeder => seeder is PhotoDeletionWorkItemSeeder), Has.Exactly(1).Items);
        Assert.That((seeders).Where(seeder => seeder is CoachingEpisodeSeeder), Has.Exactly(1).Items);
        Assert.That((seeders).Where(seeder => seeder is RecurringReportAssignmentSeeder), Has.Exactly(1).Items);
        Assert.That((seeders).Where(seeder => seeder is RecurringReportScheduleChangeSeeder), Has.Exactly(1).Items);
    }

    [Test]
    public async Task Composed_Seeders_Should_Run_In_Deterministic_Order_And_Remain_Idempotent()
    {
        var configuration = new ConfigurationBuilder().Build();
        using var provider = BuildServiceProvider(configuration);
        using var scope = provider.CreateScope();
        var seeders = scope.ServiceProvider.GetRequiredService<IEnumerable<IEntitySeeder>>().ToList();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Id<User>.New().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        var orchestrator = new SeedOrchestrator(seeders);
        var seedOptions = new SeedOptions { SeedDemoData = true };

        await orchestrator.RunAsync(context, new SeedContext(), seedOptions, CancellationToken.None);
        var firstUserCount = await context.Users.CountAsync();
        var firstRoleCount = await context.Roles.CountAsync();
        var firstRoleClaimCount = await context.RoleClaims.CountAsync();

        await orchestrator.RunAsync(context, new SeedContext(), seedOptions, CancellationToken.None);

        Assert.That(seeders, Has.Exactly(42).Items);
        Assert.That((await context.PhotoDeletionWorkItems.CountAsync()), Is.EqualTo(1));
        Assert.That((await context.PhotoDeletionWorkItems.SingleAsync()).CompletedAtUtc, Is.Not.Null);
        Assert.That((await context.Users.CountAsync()), Is.EqualTo(firstUserCount));
        Assert.That((await context.Roles.CountAsync()), Is.EqualTo(firstRoleCount));
        Assert.That((await context.RoleClaims.CountAsync()), Is.EqualTo(firstRoleClaimCount));
    }

    private static string CreateTempRepo()
    {
        var root = Path.Combine(Path.GetTempPath(), "lgym-seeder-tests", Id<DataSeederProgramTests>.New().ToString());
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "LgymApi.slnx"), string.Empty);

        var apiRoot = Path.Combine(root, "LgymApi.Api");
        Directory.CreateDirectory(apiRoot);

        var baseSettings = "{" +
                           "\"ConnectionStrings\": { \"Postgres\": \"Host=localhost\" }" +
                           "}";
        var optionalSettings = "{" +
                               "\"FeatureFlag\": \"true\"" +
                               "}";

        File.WriteAllText(Path.Combine(apiRoot, "appsettings.json"), baseSettings);
        File.WriteAllText(Path.Combine(apiRoot, "appsettings.Development.json"), optionalSettings);

        return root;
    }

    private static ServiceProvider BuildServiceProvider(IConfiguration configuration)
    {
        var method = typeof(LgymApi.DataSeeder.Program).GetMethod(
            "BuildServiceProvider",
            BindingFlags.Static | BindingFlags.NonPublic);

        return (ServiceProvider)method!
            .Invoke(null, [configuration, "Host=localhost;Database=lgym-seeder-test"])!;
    }
}
