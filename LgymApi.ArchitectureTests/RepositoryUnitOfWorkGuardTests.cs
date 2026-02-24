using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class RepositoryUnitOfWorkGuardTests
{
    [Test]
    public void Repositories_Should_Not_Call_SaveChanges_Or_BeginTransaction()
    {
        var repoRoot = ResolveRepositoryRoot();
        var repositoriesRoot = Path.Combine(repoRoot, "LgymApi.Infrastructure", "Repositories");

        Assert.That(Directory.Exists(repositoriesRoot), Is.True, $"Repositories root '{repositoriesRoot}' not found.");

        var repositoryFiles = Directory
            .EnumerateFiles(repositoriesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsInBuildArtifacts(path))
            .ToList();

        Assert.That(repositoryFiles, Is.Not.Empty, "No repositories detected for UnitOfWork guard test.");

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var violations = new List<Violation>();

        foreach (var file in repositoryFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), parseOptions, file);
            var root = tree.GetCompilationUnitRoot();

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!IsForbiddenCall(invocation))
                {
                    continue;
                }

                violations.Add(CreateViolation(repoRoot, tree, invocation));
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Repositories must not control transactions or commits. Violations:" + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    private static bool IsForbiddenCall(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var name = memberAccess.Name.Identifier.ValueText;
        return name is "SaveChanges" or "SaveChangesAsync" or "BeginTransaction" or "BeginTransactionAsync";
    }

    private static bool IsInBuildArtifacts(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
    }

    private static Violation CreateViolation(string repoRoot, SyntaxTree tree, SyntaxNode node)
    {
        var span = tree.GetLineSpan(node.Span);
        var line = span.StartLinePosition.Line + 1;
        var relativePath = Path.GetRelativePath(repoRoot, tree.FilePath);
        return new Violation(relativePath, line, node.ToString());
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
