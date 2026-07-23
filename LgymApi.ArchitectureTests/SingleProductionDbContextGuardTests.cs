using System.Diagnostics;
using System.Reflection;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class SingleProductionDbContextGuardTests
{
    private const int PersistedEntityCount = 48;
    private const string MigrationRoot = "LgymApi.Infrastructure/Migrations";

    [Test]
    public void Current_Production_Topology_Should_Have_One_Context_Model_And_Migration_Stream()
    {
        var repoRoot = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var sources = PersistenceTopologyGuardTestHelpers.LoadProductionSources(repoRoot);
        var topology = PersistenceTopologyGuardTestHelpers.Analyze(sources);
        var dbSetEntities = GetPublicDbSetEntityTypes();
        var expectedEntities = dbSetEntities.Select(type => type.Name).OrderBy(name => name, StringComparer.Ordinal).ToList();
        var configurationViolations = FindMultiplicityViolations(topology.Configurations.Select(item => item.EntityType), expectedEntities);
        var registrarViolations = FindMultiplicityViolations(topology.RegistrarEntries.Select(item => item.EntityType), expectedEntities);

        Assert.Multiple(() =>
        {
            Assert.That(sources.Select(source => source.Path), Does.Contain("LgymApi.Infrastructure/Data/AppDbContext.cs"));
            Assert.That(topology.DbContexts, Has.Count.EqualTo(1), Describe(topology.DbContexts));
            Assert.That(topology.DbContexts.Single().TypeName, Is.EqualTo(nameof(AppDbContext)), Describe(topology.DbContexts));
            Assert.That(topology.DbContexts.Single().SourcePath, Is.EqualTo("LgymApi.Infrastructure/Data/AppDbContext.cs"));
            Assert.That(dbSetEntities, Has.Count.EqualTo(PersistedEntityCount));
            Assert.That(topology.DbSets.Select(item => item.EntityType).Distinct(), Is.EquivalentTo(expectedEntities));
            Assert.That(configurationViolations, Is.Empty, string.Join(Environment.NewLine, configurationViolations));
            Assert.That(registrarViolations, Is.Empty, string.Join(Environment.NewLine, registrarViolations));
            Assert.That(topology.MigrationStreams, Has.Count.EqualTo(1), Describe(topology.MigrationStreams));
            Assert.That(topology.MigrationStreams.Single().Root, Is.EqualTo(MigrationRoot));
            Assert.That(topology.MigrationStreams.Single().SnapshotTypeNames, Is.EqualTo(new[] { "AppDbContextModelSnapshot" }));
            Assert.That(topology.MigrationStreams.Single().ContextTypeNames, Is.EqualTo(new[] { nameof(AppDbContext) }));
            Assert.That(topology.EnsureCreatedViolations, Is.Empty, Describe(topology.EnsureCreatedViolations));
            Assert.That(topology.SchemaSplitViolations, Is.Empty, Describe(topology.SchemaSplitViolations));
        });
    }

    [Test]
    public void Issue391_Worktree_Should_Not_Change_Production_Migrations()
    {
        var repoRoot = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var changedFiles = RunGit(repoRoot, ["diff", "--name-only", "HEAD", "--", MigrationRoot])
            .Concat(RunGit(repoRoot, ["ls-files", "--others", "--exclude-standard", "--", MigrationRoot]))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.That(changedFiles, Is.Empty, string.Join(Environment.NewLine, changedFiles));
    }

    [Test]
    public void Npgsql_Runtime_Model_Should_Match_The_Compiled_Snapshot_Without_A_Database_Connection()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=topology_guard;Username=guard;Password=guard")
            .Options;

        using var context = new AppDbContext(options);

        Assert.That(context.Database.ProviderName, Is.EqualTo("Npgsql.EntityFrameworkCore.PostgreSQL"));
        PersistenceTopologyGuardTestHelpers.EnsureNoPendingModelChanges(context.Database.HasPendingModelChanges());
    }

    [Test]
    public void Semantic_Fixture_Should_Detect_A_Second_Production_DbContext()
    {
        var topology = AnalyzeFixture(
            "LgymApi.Reporting/Data/ReportingDbContext.cs",
            """
            using Microsoft.EntityFrameworkCore;
            sealed class AppDbContext : DbContext { }
            sealed class ReportingDbContext : DbContext { }
            """);

        Assert.That(topology.DbContexts.Select(item => item.TypeName), Is.EquivalentTo(new[] { "AppDbContext", "ReportingDbContext" }));
    }

    [Test]
    public void Semantic_Fixtures_Should_Detect_Duplicate_And_Missing_Configurations()
    {
        var duplicate = AnalyzeFixture("LgymApi.Infrastructure/Data/Configurations/Duplicate.cs", ConfigurationFixture("UserConfiguration", "SecondUserConfiguration"));
        var missing = AnalyzeFixture("LgymApi.Infrastructure/Data/Configurations/Missing.cs", ConfigurationFixture());

        Assert.Multiple(() =>
        {
            Assert.That(duplicate.Configurations.Count(item => item.EntityType == "User"), Is.EqualTo(2));
            Assert.That(missing.Configurations.Where(item => item.EntityType == "User"), Is.Empty);
        });
    }

    [Test]
    public void Semantic_Fixtures_Should_Detect_Duplicate_And_Missing_Registrar_Entries()
    {
        var duplicate = AnalyzeFixture("LgymApi.Infrastructure/Data/Configurations/AppDbContextEntityTypeConfigurationRegistrar.cs", RegistrarFixture("Register(new UserConfiguration()); Register(new UserConfiguration());"));
        var missing = AnalyzeFixture("LgymApi.Infrastructure/Data/Configurations/AppDbContextEntityTypeConfigurationRegistrar.cs", RegistrarFixture(string.Empty));

        Assert.Multiple(() =>
        {
            Assert.That(duplicate.RegistrarEntries.Count(item => item.EntityType == "User"), Is.EqualTo(2));
            Assert.That(missing.RegistrarEntries.Where(item => item.EntityType == "User"), Is.Empty);
        });
    }

    [Test]
    public void Project_Path_Fixture_Should_Detect_A_Second_Migration_Stream()
    {
        var topology = PersistenceTopologyGuardTestHelpers.Analyze(
        [
            new TopologySource("LgymApi.Infrastructure/Migrations/Initial.cs", MigrationFixture("Initial")),
            new TopologySource("LgymApi.Reporting/Migrations/Initial.cs", MigrationFixture("ReportingInitial"))
        ]);

        Assert.That(topology.MigrationStreams.Select(item => item.Root), Is.EquivalentTo(new[] { MigrationRoot, "LgymApi.Reporting/Migrations" }));
    }

    [Test]
    public void Semantic_Fixture_Should_Detect_A_Production_Schema_Split()
    {
        var topology = AnalyzeFixture(
            "LgymApi.Infrastructure/Data/SchemaConfiguration.cs",
            "using Microsoft.EntityFrameworkCore; sealed class SchemaConfiguration { void Apply(ModelBuilder modelBuilder) => modelBuilder.HasDefaultSchema(\"workouts\"); }");

        Assert.That(topology.SchemaSplitViolations, Has.Count.EqualTo(1));
    }

    [Test]
    public void Semantic_Fixtures_Should_Reject_Production_EnsureCreated_And_Preserve_NonRelational_Test_Setup()
    {
        var production = AnalyzeFixture(
            "LgymApi.Infrastructure/Data/Bootstrap.cs",
            "using Microsoft.EntityFrameworkCore; sealed class Bootstrap : DbContext { void Run() => Database.EnsureCreated(); }");
        var nonRelational = AnalyzeFixture(
            "LgymApi.DataSeeder/SeedOrchestrator.cs",
            "using Microsoft.EntityFrameworkCore; sealed class SeedContext : DbContext { void Run() { if (!Database.IsRelational()) { Database.EnsureCreated(); } } }");

        Assert.Multiple(() =>
        {
            Assert.That(production.EnsureCreatedViolations, Has.Count.EqualTo(1));
            Assert.That(nonRelational.EnsureCreatedViolations, Is.Empty);
        });
    }

    [Test]
    public void Snapshot_Drift_Fixture_Should_Be_Rejected()
    {
        Assert.That(
            () => PersistenceTopologyGuardTestHelpers.EnsureNoPendingModelChanges(true),
            Throws.InvalidOperationException.With.Message.Contains("AppDbContextModelSnapshot"));
    }

    private static PersistenceTopologyAnalysis AnalyzeFixture(string path, string source)
    {
        return PersistenceTopologyGuardTestHelpers.Analyze([new TopologySource(path, source)]);
    }

    private static IReadOnlyList<Type> GetPublicDbSetEntityTypes()
    {
        return typeof(AppDbContext).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(property => property.PropertyType.GenericTypeArguments[0])
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> FindMultiplicityViolations(IEnumerable<string> actualEntities, IReadOnlyCollection<string> expectedEntities)
    {
        var counts = actualEntities.GroupBy(entity => entity, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        return expectedEntities.Where(entity => !counts.TryGetValue(entity, out var count) || count != 1)
            .Concat(counts.Keys.Where(entity => !expectedEntities.Contains(entity, StringComparer.Ordinal)))
            .OrderBy(entity => entity, StringComparer.Ordinal)
            .Select(entity => $"{entity}: found {counts.GetValueOrDefault(entity, 0)} entries")
            .ToList();
    }

    private static string Describe<T>(IEnumerable<T> values) => string.Join(Environment.NewLine, values);

    private static IReadOnlyList<string> RunGit(string repoRoot, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start git.");
        if (!process.WaitForExit(10_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"git {string.Join(' ', arguments)} did not finish within 10 seconds.");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        Assert.That(process.ExitCode, Is.Zero, error);
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string ConfigurationFixture(params string[] configurationTypes)
    {
        var configurations = string.Join(Environment.NewLine, configurationTypes.Select(name =>
            $"sealed class {name} : IEntityTypeConfiguration<User> {{ public void Configure(EntityTypeBuilder<User> builder) {{ }} }}"));
        return $"using Microsoft.EntityFrameworkCore; using Microsoft.EntityFrameworkCore.Metadata.Builders; class User {{ }} sealed class AppDbContext : DbContext {{ public DbSet<User> Users => Set<User>(); }} {configurations}";
    }

    private static string RegistrarFixture(string registrations)
    {
        return $"using Microsoft.EntityFrameworkCore; using Microsoft.EntityFrameworkCore.Metadata.Builders; class User {{ }} sealed class UserConfiguration : IEntityTypeConfiguration<User> {{ public void Configure(EntityTypeBuilder<User> builder) {{ }} }} static class AppDbContextEntityTypeConfigurationRegistrar {{ static void Register<T>(IEntityTypeConfiguration<T> configuration) {{ }} static void Apply() {{ {registrations} }} }}";
    }

    private static string MigrationFixture(string typeName)
    {
        return $"using Microsoft.EntityFrameworkCore; using Microsoft.EntityFrameworkCore.Infrastructure; using Microsoft.EntityFrameworkCore.Migrations; sealed class AppDbContext : DbContext {{ }} [DbContext(typeof(AppDbContext))] sealed class {typeName} : Migration {{ protected override void Up(MigrationBuilder builder) {{ }} protected override void Down(MigrationBuilder builder) {{ }} }}";
    }
}
