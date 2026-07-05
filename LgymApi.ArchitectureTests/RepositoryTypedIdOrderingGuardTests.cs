using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class RepositoryTypedIdOrderingGuardTests
{
    private static readonly HashSet<string> OrderingMethods = new(StringComparer.Ordinal)
    {
        "OrderBy",
        "OrderByDescending",
        "ThenBy",
        "ThenByDescending"
    };

    [Test]
    public void Repositories_Should_Not_Unwrap_TypedIds_Inside_Linq_Ordering()
    {
        var repoRoot = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var repositoriesRoot = Path.Combine(repoRoot, "LgymApi.Infrastructure", "Repositories");

        Assert.That(Directory.Exists(repositoriesRoot), Is.True, $"Repositories root '{repositoriesRoot}' not found.");

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var sourceFiles = Directory
            .EnumerateFiles(repoRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !ArchitectureTestHelpers.IsInBuildArtifacts(path))
            .ToList();

        Assert.That(sourceFiles, Is.Not.Empty, "No C# source files found for typed-id ordering guard.");

        var syntaxTrees = sourceFiles
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), options: parseOptions, path: path))
            .ToList();

        var compilation = ArchitectureTestHelpers.CreateCompilation(syntaxTrees);
        var violations = new List<Violation>();

        foreach (var tree in syntaxTrees.Where(tree => IsRepositoryPath(tree.FilePath, repositoriesRoot)))
        {
            var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            var root = tree.GetCompilationUnitRoot();

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!IsOrderingInvocation(invocation))
                {
                    continue;
                }

                foreach (var lambda in invocation.ArgumentList.Arguments.Select(argument => argument.Expression).OfType<LambdaExpressionSyntax>())
                {
                    CollectViolations(repoRoot, tree, semanticModel, lambda.Body, violations);
                }
            }
        }

        Assert.That(
            violations.Distinct().ToList(),
            Is.Empty,
            "Repositories must order by typed IDs directly. Avoid Id<T>.Value / Id<T>.GetValue() inside OrderBy/ThenBy selectors."
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations.Distinct().Select(v => v.ToString())));
    }

    private static void CollectViolations(
        string repoRoot,
        SyntaxTree tree,
        SemanticModel semanticModel,
        CSharpSyntaxNode selectorBody,
        List<Violation> violations)
    {
        foreach (var memberAccess in selectorBody.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
        {
            if (!string.Equals(memberAccess.Name.Identifier.ValueText, "Value", StringComparison.Ordinal))
            {
                continue;
            }

            var receiverType = semanticModel.GetTypeInfo(memberAccess.Expression).Type;

            if (IsTypedId(receiverType))
            {
                violations.Add(CreateViolation(
                    repoRoot,
                    tree,
                    memberAccess,
                    "Typed ID unwrapped via .Value inside LINQ ordering; order by the Id<T> directly."));
                continue;
            }

            if (IsNullableTypedId(receiverType))
            {
                violations.Add(CreateViolation(
                    repoRoot,
                    tree,
                    memberAccess,
                    "Nullable typed ID unwrapped via .Value inside LINQ ordering; keep ordering on the typed ID expression."));
            }
        }

        foreach (var nestedInvocation in selectorBody.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (nestedInvocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            if (!string.Equals(memberAccess.Name.Identifier.ValueText, "GetValue", StringComparison.Ordinal))
            {
                continue;
            }

            var receiverType = semanticModel.GetTypeInfo(memberAccess.Expression).Type;
            if (!IsTypedId(receiverType))
            {
                continue;
            }

            violations.Add(CreateViolation(
                repoRoot,
                tree,
                nestedInvocation,
                "Typed ID unwrapped via .GetValue() inside LINQ ordering; order by the Id<T> directly."));
        }
    }

    private static bool IsOrderingInvocation(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        return OrderingMethods.Contains(memberAccess.Name.Identifier.ValueText);
    }

    private static bool IsRepositoryPath(string filePath, string repositoriesRoot)
    {
        var normalizedFile = Path.GetFullPath(filePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRoot = Path.GetFullPath(repositoriesRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return normalizedFile.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedFile, normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTypedId(ITypeSymbol? typeSymbol)
    {
        return typeSymbol is INamedTypeSymbol namedType
            && string.Equals(namedType.Name, "Id", StringComparison.Ordinal)
            && namedType.Arity == 1
            && string.Equals(namedType.ContainingNamespace?.ToDisplayString(), "LgymApi.Domain.ValueObjects", StringComparison.Ordinal);
    }

    private static bool IsNullableTypedId(ITypeSymbol? typeSymbol)
    {
        return typeSymbol is INamedTypeSymbol namedType
            && string.Equals(namedType.ContainingNamespace?.ToDisplayString(), "System", StringComparison.Ordinal)
            && string.Equals(namedType.Name, "Nullable", StringComparison.Ordinal)
            && namedType.TypeArguments.Length == 1
            && IsTypedId(namedType.TypeArguments[0]);
    }

    private static Violation CreateViolation(string repoRoot, SyntaxTree tree, SyntaxNode node, string message)
    {
        var span = tree.GetLineSpan(node.Span);
        var line = span.StartLinePosition.Line + 1;
        var relativePath = Path.GetRelativePath(repoRoot, tree.FilePath);
        return new Violation(relativePath, line, message, node.ToString());
    }

    private sealed record Violation(string File, int Line, string Message, string Expression)
    {
        public override string ToString() => $"{File}:{Line} {Message} Expression: {Expression}";
    }
}
