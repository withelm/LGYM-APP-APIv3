using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;

namespace LgymApi.ExternalE2ETests.Harness;

[TestFixture]
[Category("SourcePolicy")]
public sealed class ExternalE2ESourcePolicyTests
{
    private const string ExternalProjectDirectory = "LgymApi.ExternalE2ETests";
    private static readonly string[] ForbiddenReferences =
    [
        "LgymApi.E2ETests",
        "LgymApi.Api",
        "LgymApi.Application",
        "LgymApi.Domain",
        "LgymApi.Infrastructure",
        "LgymApi.Mobile"
    ];

    [Test]
    public void Test_external_production_sources_when_scanned_reject_product_references_and_unsafe_output()
    {
        var violations = LoadProductionSources()
            .SelectMany(FindForbiddenReferences)
            .Concat(LoadProductionSources().SelectMany(FindUnsafeOutputOrConfiguration))
            .ToArray();

        Assert.That(violations, Is.Empty);
    }

    [TestCase("LgymApi.E2ETests")]
    [TestCase("LgymApi.Api")]
    public void Test_source_policy_when_legacy_or_product_reference_or_raw_output_is_present_rejects_it(string forbiddenReference)
    {
        var source = new SourceFile(
            "unsafe.cs",
            $"using {forbiddenReference}; Console.WriteLine(options.ConnectionString); AddUserSecrets();");

        var violations = FindForbiddenReferences(source)
            .Concat(FindUnsafeOutputOrConfiguration(source))
            .ToArray();

        Assert.That(
            violations,
            Is.EquivalentTo([
                $"unsafe.cs: forbidden reference {forbiddenReference}",
                "unsafe.cs: unsafe output or configuration Console.Write",
                "unsafe.cs: unsafe output or configuration AddUserSecrets"
            ]));
    }

    [Test]
    public void Test_external_production_sources_when_processes_are_used_only_allow_postgresql_tools()
    {
        var sources = LoadProductionSources();
        var violations = sources.SelectMany(FindUnsafeProcessLaunches).ToArray();
        var postgreSqlRunner = sources.Single(source => source.Name == "PostgreSqlToolProcess.cs").Content;

        Assert.Multiple(() =>
        {
            Assert.That(violations, Is.Empty);
            Assert.That(postgreSqlRunner, Does.Contain("UseShellExecute = false"));
            Assert.That(postgreSqlRunner, Does.Contain("ArgumentList.Add(argument)"));
            Assert.That(postgreSqlRunner, Does.Contain("\"psql\" : \"pg_restore\""));
            Assert.That(postgreSqlRunner, Does.Not.Contain("dotnet run"));
        });
    }

    [Test]
    public void Test_source_policy_when_api_or_mobile_process_launch_is_present_rejects_it()
    {
        var source = new SourceFile(
            "unsafe.cs",
            "using System.Diagnostics; Process.Start(\"dotnet\", \"run --project LgymApi.Api\");");

        Assert.That(
            FindUnsafeProcessLaunches(source),
            Is.EquivalentTo(["unsafe.cs: process launch is not permitted"]));
    }

    [Test]
    public void Test_restore_source_when_scanned_requires_validated_target_and_target_only_session_scope()
    {
        var source = LoadProductionSources().Single(item => item.Name == "PostgreSqlRestoreProtocol.cs").Content;

        Assert.Multiple(() =>
        {
            Assert.That(FindUnsafeRestoreTargets(new SourceFile("restore.cs", source)), Is.Empty);
            Assert.That(source, Does.Contain("^lgym_external_e2e(?:_[a-z0-9]+)*$"));
            Assert.That(source, Does.Contain("datname = current_database()"));
            Assert.That(source, Does.Contain("datname = :'expected_database'"));
            Assert.That(source, Does.Contain("pid <> pg_backend_pid()"));
        });
    }

    [Test]
    public void Test_source_policy_when_restore_target_is_literal_or_broad_rejects_it()
    {
        var source = new SourceFile(
            "unsafe.cs",
            "[\"--dbname\", \"postgres\"]; DROP DATABASE production;");

        Assert.That(
            FindUnsafeRestoreTargets(source),
            Is.EquivalentTo([
                "unsafe.cs: restore target must be validated rather than literal",
                "unsafe.cs: broad restore operation is not permitted"
            ]));
    }

    [Test]
    public void Test_safe_metadata_when_scanned_preserves_isolated_project_and_private_configuration_contract()
    {
        var repositoryRoot = RepositoryRoot.Resolve();
        var projectPath = Path.Combine(repositoryRoot, ExternalProjectDirectory, "LgymApi.ExternalE2ETests.csproj");
        var projectDocument = XDocument.Load(projectPath);
        var ignoreRules = File.ReadAllText(Path.Combine(repositoryRoot, ".gitignore"));
        var examplePath = Path.Combine(repositoryRoot, ExternalProjectDirectory, "appsettings.ExternalE2E.example.json");
        using var example = JsonDocument.Parse(File.ReadAllText(examplePath));

        Assert.Multiple(() =>
        {
            Assert.That(projectDocument.Descendants().Where(item => item.Name.LocalName == "ProjectReference"), Is.Empty);
            Assert.That(projectDocument.ToString(), Does.Not.Contain("LgymApi.E2ETests"));
            Assert.That(ignoreRules, Does.Contain("!LgymApi.ExternalE2ETests/appsettings.ExternalE2E.example.json"));
            Assert.That(ignoreRules, Does.Contain("LgymApi.ExternalE2ETests/appsettings.ExternalE2E.json"));
            Assert.That(ignoreRules, Does.Contain("LgymApi.ExternalE2ETests/*.bak"));
            Assert.That(example.RootElement.GetProperty("ConnectionString").GetString(), Is.Empty);
            Assert.That(example.RootElement.GetProperty("BaselineDumpPath").GetString(), Is.Empty);
            Assert.That(example.RootElement.GetProperty("ArtifactRoot").GetString(), Is.Empty);
        });
    }

    private static IReadOnlyList<SourceFile> LoadProductionSources()
    {
        var projectDirectory = Path.Combine(RepositoryRoot.Resolve(), ExternalProjectDirectory);
        return Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.EndsWith("Tests.cs", StringComparison.Ordinal))
            .Select(path => new SourceFile(Path.GetFileName(path), File.ReadAllText(path)))
            .ToArray();
    }

    private static IEnumerable<string> FindForbiddenReferences(SourceFile source) =>
        ForbiddenReferences
            .Where(reference => source.Content.Contains(reference, StringComparison.Ordinal))
            .Select(reference => $"{source.Name}: forbidden reference {reference}");

    private static IEnumerable<string> FindUnsafeOutputOrConfiguration(SourceFile source)
    {
        var unsafeTokens = new[] { "Console.Write", "TestContext.Progress", "TestContext.Out", "AddUserSecrets" };
        return unsafeTokens
            .Where(token => source.Content.Contains(token, StringComparison.Ordinal))
            .Select(token => $"{source.Name}: unsafe output or configuration {token}");
    }

    private static IEnumerable<string> FindUnsafeProcessLaunches(SourceFile source)
    {
        var launchesProcess = source.Content.Contains("Process.Start(", StringComparison.Ordinal) ||
                              source.Content.Contains("new ProcessStartInfo", StringComparison.Ordinal);
        var isPostgreSqlRunner = source.Name == "PostgreSqlToolProcess.cs";

        return launchesProcess && !isPostgreSqlRunner
            ? [$"{source.Name}: process launch is not permitted"]
            : [];
    }

    private static IEnumerable<string> FindUnsafeRestoreTargets(SourceFile source)
    {
        if (source.Content.Contains("DROP DATABASE", StringComparison.OrdinalIgnoreCase) ||
            source.Content.Contains("dropdb", StringComparison.OrdinalIgnoreCase))
        {
            yield return $"{source.Name}: broad restore operation is not permitted";
        }

        if (Regex.IsMatch(source.Content, "--dbname\\\",\\s*\\\"[^\\\"]+\\\"", RegexOptions.CultureInvariant))
        {
            yield return $"{source.Name}: restore target must be validated rather than literal";
        }
    }

    private sealed record SourceFile(string Name, string Content);
}
