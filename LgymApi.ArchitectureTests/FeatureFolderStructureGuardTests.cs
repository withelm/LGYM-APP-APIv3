namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class FeatureFolderStructureGuardTests
{
    [Test]
    public void Features_With_Controllers_Should_Contain_Only_Contracts_Controllers_Validation_Folders()
    {
        var repoRoot = ResolveRepositoryRoot();
        var featuresRoot = Path.Combine(repoRoot, "LgymApi.Api", "Features");

        Assert.That(
            Directory.Exists(featuresRoot),
            Is.True,
            $"Features root directory '{featuresRoot}' does not exist.");

        var featureDirectories = Directory
            .EnumerateDirectories(featuresRoot, "*", SearchOption.TopDirectoryOnly)
            .ToList();

        Assert.That(
            featureDirectories,
            Is.Not.Empty,
            $"No feature directories found in '{featuresRoot}'.");

        var expectedSubfolders = new HashSet<string>(StringComparer.Ordinal)
        {
            "Contracts",
            "Controllers",
            "Validation"
        };

        var violations = new List<Violation>();

        foreach (var featureDirectory in featureDirectories)
        {
            var featureName = Path.GetFileName(featureDirectory);

            var hasController = Directory
                .EnumerateFiles(featureDirectory, "*Controller.cs", SearchOption.AllDirectories)
                .Any();

            if (!hasController)
            {
                continue;
            }

            var actualSubfolders = Directory
                .EnumerateDirectories(featureDirectory, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToHashSet(StringComparer.Ordinal);

            if (!actualSubfolders.SetEquals(expectedSubfolders))
            {
                var relativeFeaturePath = Path.GetRelativePath(repoRoot, featureDirectory);
                violations.Add(new Violation(relativeFeaturePath, expectedSubfolders, actualSubfolders));
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Each feature with controllers must contain exactly 'Contracts', 'Controllers', 'Validation' subfolders. Violations count: " + violations.Count + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
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

    private sealed record Violation(string FeaturePath, HashSet<string> Expected, HashSet<string> Actual)
    {
        public override string ToString()
        {
            var expected = string.Join(", ", Expected.OrderBy(x => x, StringComparer.Ordinal));
            var actual = string.Join(", ", Actual.OrderBy(x => x, StringComparer.Ordinal));
            return $"{FeaturePath} [expected: {expected}] [actual: {actual}]";
        }
    }
}
