using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;

namespace LgymApi.ExternalE2ETests.Harness;

[TestFixture]
[Category("Topology")]
public sealed class StandaloneTopologyTests
{
    private const string LegacyE2EProjectDirectory = "LgymApi.E2ETests";

    [Test]
    public void Test_standalone_solution_and_project_when_harness_is_isolated()
    {
        var repositoryRoot = RepositoryRoot.Resolve();
        var standaloneSolution = Path.Combine(repositoryRoot, "LgymApi.ExternalE2ETests.sln");
        var standaloneProject = Path.Combine(repositoryRoot, "LgymApi.ExternalE2ETests", "LgymApi.ExternalE2ETests.csproj");
        var projects = ParseSolutionProjects(standaloneSolution);
        var projectDocument = XDocument.Load(standaloneProject);

        Assert.Multiple(() =>
        {
            Assert.That(projects, Is.EqualTo(new[] { "LgymApi.ExternalE2ETests\\LgymApi.ExternalE2ETests.csproj" }));
            Assert.That(GetPropertyValue(projectDocument, "TargetFramework"), Is.EqualTo("net10.0"));
            Assert.That(GetPropertyValue(projectDocument, "IsTestProject"), Is.EqualTo("true"));
            Assert.That(GetItems(projectDocument, "ProjectReference"), Is.Empty);
            Assert.That(GetItems(projectDocument, "PackageReference").Select(GetInclude), Does.Contain("Microsoft.Playwright"));
            Assert.That(GetItems(projectDocument, "PackageReference").Select(GetInclude), Does.Contain("Reqnroll.NUnit"));
            Assert.That(GetItems(projectDocument, "PackageReference").Select(GetVersion), Is.All.Null.Or.Empty);
            Assert.That(FindLegacyE2EProjectPaths(projectDocument), Is.Empty);
        });
    }

    [TestCase("Import", "Project")]
    [TestCase("Compile", "Include")]
    [TestCase("Content", "Include")]
    [TestCase("Compile", "Link")]
    [TestCase("Content", "Link")]
    [TestCase("None", "Include")]
    public void Test_project_file_item_when_legacy_e2e_path_is_present_is_rejected(string itemName, string referenceName)
    {
        var fixtureDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, Path.GetRandomFileName());
        Directory.CreateDirectory(fixtureDirectory);

        try
        {
            var fixturePath = Path.Combine(fixtureDirectory, "fixture.csproj");
            File.WriteAllText(fixturePath, CreateLegacyE2EProjectFixture(itemName, referenceName));

            var violations = FindLegacyE2EProjectPaths(XDocument.Load(fixturePath));

            Assert.That(violations, Is.EquivalentTo(new[] { $"{itemName}.{referenceName}" }));
        }
        finally
        {
            Directory.Delete(fixtureDirectory, recursive: true);
        }
    }

    [Test]
    public void Test_evaluated_project_references_when_harness_is_package_only()
    {
        var repositoryRoot = RepositoryRoot.Resolve();
        var standaloneProject = Path.Combine(repositoryRoot, "LgymApi.ExternalE2ETests", "LgymApi.ExternalE2ETests.csproj");
        var evaluation = RunMsBuildProjectReferenceQuery(standaloneProject, repositoryRoot);

        Assert.That(evaluation.ExitCode, Is.EqualTo(0), evaluation.StandardError);

        using var document = JsonDocument.Parse(evaluation.StandardOutput);
        var references = document.RootElement
            .GetProperty("Items")
            .GetProperty("ProjectReference");

        Assert.That(references.GetArrayLength(), Is.Zero);
    }

    [Test]
    public void Test_main_solution_topology_when_standalone_harness_exists_remains_baselined()
    {
        var repositoryRoot = RepositoryRoot.Resolve();
        var mainSolution = Path.Combine(repositoryRoot, "LgymApi.sln");
        var solutionProjects = ParseSolutionProjects(mainSolution);
        var projectReferences = solutionProjects.Sum(project =>
        {
            var projectFile = Path.Combine(repositoryRoot, project);
            return GetItems(XDocument.Load(projectFile), "ProjectReference").Count;
        });

        Assert.Multiple(() =>
        {
            Assert.That(solutionProjects, Has.Count.EqualTo(18));
            Assert.That(projectReferences, Is.EqualTo(90));
            Assert.That(File.ReadAllText(mainSolution), Does.Not.Contain("LgymApi.ExternalE2ETests"));
        });
    }

    private static IReadOnlyList<string> ParseSolutionProjects(string solutionPath)
    {
        var projectPattern = new Regex(
            "Project\\(\\\"[^\\\"]+\\\"\\) = \\\"[^\\\"]+\\\", \\\"(?<path>[^\\\"]+\\.csproj)\\\", \\\"[^\\\"]+\\\"",
            RegexOptions.CultureInvariant);

        return File.ReadLines(solutionPath)
            .Select(line => projectPattern.Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups["path"].Value)
            .ToArray();
    }

    private static IReadOnlyList<XElement> GetItems(XDocument projectDocument, string itemName) =>
        projectDocument.Descendants().Where(element => element.Name.LocalName == itemName).ToArray();

    private static string? GetPropertyValue(XDocument projectDocument, string propertyName) =>
        projectDocument.Descendants().Single(element => element.Name.LocalName == propertyName).Value;

    private static string? GetInclude(XElement item) => item.Attribute("Include")?.Value;

    private static string? GetVersion(XElement item) =>
        item.Attribute("Version")?.Value ?? item.Elements().SingleOrDefault(element => element.Name.LocalName == "Version")?.Value;

    private static string CreateLegacyE2EProjectFixture(string itemName, string referenceName)
    {
        const string legacyPath = "..\\LgymApi.E2ETests\\legacy.fixture";

        var item = referenceName == "Link"
            ? $"<{itemName} Include=\"fixture.cs\"><Link>{legacyPath}</Link></{itemName}>"
            : $"<{itemName} {referenceName}=\"{legacyPath}\" />";
        var projectContents = itemName == "Import"
            ? item
            : $"<ItemGroup>{item}</ItemGroup>";

        return $"<Project>{projectContents}</Project>";
    }

    private static IReadOnlyList<string> FindLegacyE2EProjectPaths(XDocument projectDocument)
    {
        return projectDocument.Descendants()
            .SelectMany(element => element.Attributes()
                .Where(attribute => ReferencesLegacyE2EProject(attribute.Value))
                .Select(attribute => $"{element.Name.LocalName}.{attribute.Name.LocalName}")
                .Concat(element.Elements()
                    .Where(metadata => !metadata.HasElements && ReferencesLegacyE2EProject(metadata.Value))
                    .Select(metadata => $"{element.Name.LocalName}.{metadata.Name.LocalName}")))
            .OrderBy(violation => violation, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool ReferencesLegacyE2EProject(string value) =>
        value.Contains(LegacyE2EProjectDirectory, StringComparison.OrdinalIgnoreCase);

    private static ProcessResult RunMsBuildProjectReferenceQuery(string projectPath, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo("dotnet", $"msbuild \"{projectPath}\" -getItem:ProjectReference -nologo")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start the dotnet MSBuild process.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
