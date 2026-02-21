using System.Xml.Linq;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class CentralPackageManagementGuardTests
{
    [Test]
    public void ProjectFiles_ShouldNotDefinePackageReferenceVersions()
    {
        var repoRoot = ResolveRepositoryRoot();
        var projectFiles = Directory
            .EnumerateFiles(repoRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsInBuildArtifacts(path))
            .ToList();

        Assert.That(projectFiles, Is.Not.Empty, "No project files found in repository.");

        var violations = new List<Violation>();

        foreach (var projectFile in projectFiles)
        {
            var document = XDocument.Load(projectFile);
            var packageReferences = document
                .Descendants()
                .Where(node => node.Name.LocalName == "PackageReference");

            foreach (var packageReference in packageReferences)
            {
                var packageName = packageReference.Attribute("Include")?.Value ?? "<unknown>";
                var hasVersionAttribute = !string.IsNullOrWhiteSpace(packageReference.Attribute("Version")?.Value);
                var hasVersionElement = packageReference
                    .Elements()
                    .Any(element => element.Name.LocalName == "Version" && !string.IsNullOrWhiteSpace(element.Value));

                if (!hasVersionAttribute && !hasVersionElement)
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(repoRoot, projectFile);
                violations.Add(new Violation(relativePath, packageName));
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Package versions must be managed only in Directory.Packages.props. Violations count: " + violations.Count + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    private static bool IsInBuildArtifacts(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "LgymApi.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    private sealed record Violation(string File, string Package)
    {
        public override string ToString() => $"{File} [{Package}]";
    }
}
