using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class ResultPatternMigrationGuardTests
{
    // Baseline established from current codebase state (2026-04-03).
    // This is the known count of forbidden AppException throws (excluding AppException.Internal).
    // The guard prevents NEW violations from being introduced during Result pattern migration.
    // As services are converted, this baseline should DECREASE toward zero.
    private const int BaselineViolationCount = 204;

    [Test]
    public void Application_Services_Should_Not_Introduce_New_AppException_Throws()
    {
        var repoRoot = ResolveRepositoryRoot();
        var applicationRoot = Path.Combine(repoRoot, "LgymApi.Application");

        Assert.That(Directory.Exists(applicationRoot), Is.True, $"Application root '{applicationRoot}' not found.");

        var serviceFiles = Directory
            .EnumerateFiles(applicationRoot, "*Service.cs", SearchOption.AllDirectories)
            .Where(path => !IsInBuildArtifacts(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.That(serviceFiles, Is.Not.Empty, "No service files found for Result pattern migration guard test.");

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var violations = new List<Violation>();

        foreach (var serviceFile in serviceFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(serviceFile), parseOptions, serviceFile);
            var root = tree.GetCompilationUnitRoot();

            // Find all throw statements that throw AppException
            foreach (var throwStatement in root.DescendantNodes().OfType<ThrowStatementSyntax>())
            {
                if (throwStatement.Expression is InvocationExpressionSyntax invocation)
                {
                    if (IsAppExceptionThrow(invocation) && !IsExcludedAppException(invocation))
                    {
                        violations.Add(CreateViolation(repoRoot, tree, throwStatement));
                    }
                }
            }
        }

        // Sort violations deterministically by file path and line number for stable ordering across runs
        violations.Sort((a, b) =>
        {
            var fileCompare = string.Compare(a.File, b.File, StringComparison.Ordinal);
            return fileCompare != 0 ? fileCompare : a.Line.CompareTo(b.Line);
        });

        // Regression guard: violation count must NOT exceed baseline.
        // This prevents new AppException throws from being introduced.
        // As services are converted to Result pattern, baseline should decrease.
        Assert.That(
            violations.Count,
            Is.LessThanOrEqualTo(BaselineViolationCount),
            $"Result pattern migration regression detected. Forbidden AppException throws increased from {BaselineViolationCount} to {violations.Count}. " +
            "New violations (not in baseline):" + Environment.NewLine +
            FormatNewViolations(violations, BaselineViolationCount));
    }

    private static bool IsAppExceptionThrow(InvocationExpressionSyntax invocation)
    {
        // Check if this is a call to AppException.Something(...)
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Expression is IdentifierNameSyntax identifier &&
                identifier.Identifier.ValueText == "AppException")
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsExcludedAppException(InvocationExpressionSyntax invocation)
    {
        // Allow AppException.Internal (transitional exception, being phased out)
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var methodName = memberAccess.Name.Identifier.ValueText;
            return methodName == "Internal";
        }

        return false;
    }

    private static string FormatNewViolations(List<Violation> violations, int baselineCount)
    {
        if (violations.Count <= baselineCount)
        {
            return "(none - count is within baseline)";
        }

        // Create a stable set of baseline violations to identify which ones are new.
        // This approach is deterministic: sort violations, identify first baselineCount as "known", rest are "new".
        var newViolations = violations.Skip(baselineCount).ToList();
        return "New violations beyond baseline:\n" + string.Join(Environment.NewLine, newViolations.Select(v => v.ToString()));
    }

    private static bool IsInBuildArtifacts(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
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
