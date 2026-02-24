using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class UnitOfWorkCommitGuardTests
{
    private static readonly string[] AllowedSegments =
    {
        "/LgymApi.Application/",
        "/LgymApi.Infrastructure/UnitOfWork/",
        "/LgymApi.Infrastructure/Data/",
        "/LgymApi.ArchitectureTests/"
    };

    private static readonly string[] TestSegments =
    {
        "/LgymApi.UnitTests/",
        "/LgymApi.IntegrationTests/",
        "/LgymApi.ArchitectureTests/"
    };

    [Test]
    public void SaveChanges_Should_Be_Invoked_Only_From_Service_Layer()
    {
        var repoRoot = ResolveRepositoryRoot();
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var sourceFiles = Directory
            .EnumerateFiles(repoRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsInBuildArtifacts(path))
            .ToList();

        Assert.That(sourceFiles, Is.Not.Empty, "No C# files found for UnitOfWork guard test.");

        var violations = new List<Violation>();

        foreach (var file in sourceFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), options: parseOptions, path: file);
            var root = tree.GetCompilationUnitRoot();

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!IsUowMethod(invocation))
                {
                    continue;
                }

                if (IsAllowedPath(file))
                {
                    continue;
                }

                violations.Add(CreateViolation(repoRoot, tree, invocation));
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "IUnitOfWork SaveChanges/transactions must be invoked only from application services. Violations: " + violations.Count + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    private static bool IsUowMethod(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        return memberAccess.Name.Identifier.ValueText is "SaveChangesAsync" or "BeginTransactionAsync";
    }

    private static bool IsAllowedPath(string path)
    {
        var normalized = Normalize(path);

        if (IsTestPath(normalized))
        {
            return true;
        }

        return AllowedSegments.Any(segment => normalized.Contains(segment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTestPath(string normalizedPath)
    {
        return TestSegments.Any(segment => normalizedPath.Contains(segment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsInBuildArtifacts(string path)
    {
        var normalized = Normalize(path);
        return normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static Violation CreateViolation(string repoRoot, SyntaxTree tree, SyntaxNode node)
    {
        var span = tree.GetLineSpan(node.Span);
        var line = span.StartLinePosition.Line + 1;
        var relativePath = Path.GetRelativePath(repoRoot, tree.FilePath);
        var member = node.ToString();
        return new Violation(relativePath, line, member);
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

    private sealed record Violation(string File, int Line, string Expression)
    {
        public override string ToString() => $"{File}:{Line} -> {Expression}";
    }
}
