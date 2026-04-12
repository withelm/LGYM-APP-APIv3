using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class ValidationMessageResourceGuardTests
{
    /// <summary>
    /// Ensures that FluentValidation validators use Messages resource keys instead of hardcoded strings.
    /// All `.WithMessage(...)` calls should reference LgymApi.Resources.Messages.* keys, not hardcoded strings.
    /// Allowed: Messages.KeyName, $"{Messages.KeyName} {variable}", nameof(Messages.KeyName)
    /// Forbidden: .WithMessage("hardcoded string")
    /// </summary>
    [Test]
    public void Validators_Should_Use_Message_Resources_Not_Hardcoded_Strings()
    {
        var repoRoot = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var apiRoot = Path.Combine(repoRoot, "LgymApi.Api");

        Assert.That(Directory.Exists(apiRoot), Is.True, $"LgymApi.Api root '{apiRoot}' not found.");

        var validatorFiles = Directory
            .EnumerateFiles(apiRoot, "*Validator.cs", SearchOption.AllDirectories)
            .Where(path => !ArchitectureTestHelpers.IsInBuildArtifacts(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.That(validatorFiles, Is.Not.Empty, "No validator files found for validation message resource guard test.");

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var violations = new List<Violation>();

        foreach (var validatorFile in validatorFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(validatorFile), parseOptions, validatorFile);
            var root = tree.GetCompilationUnitRoot();

            // Find all invocations of WithMessage
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (IsWithMessageInvocation(invocation))
                {
                    // Check if the argument is a hardcoded string literal (violation)
                    if (HasHardcodedStringArgument(invocation))
                    {
                        violations.Add(CreateViolation(repoRoot, tree, invocation));
                    }
                }
            }
        }

        violations.Sort((a, b) =>
        {
            var fileCompare = string.Compare(a.File, b.File, StringComparison.Ordinal);
            return fileCompare != 0 ? fileCompare : a.Line.CompareTo(b.Line);
        });

        Assert.That(
            violations.Count,
            Is.EqualTo(0),
            $"Validators must use Messages resource keys, not hardcoded strings. " +
            $"Found {violations.Count} violations:\n" +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    /// <summary>
    /// Checks if an invocation is a call to WithMessage (either chained or direct).
    /// </summary>
    private static bool IsWithMessageInvocation(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.ValueText == "WithMessage";
        }

        return false;
    }

    /// <summary>
    /// Checks if the first argument to WithMessage is a hardcoded string literal.
    /// Returns false for:
    /// - Member access expressions (Messages.KeyName)
    /// - Interpolated strings that use Messages.KeyName
    /// - Method invocations (nameof expressions, etc.)
    /// Returns true only for pure string literals.
    /// </summary>
    private static bool HasHardcodedStringArgument(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        var firstArg = invocation.ArgumentList.Arguments[0].Expression;

        // Check if it's a string literal (hardcoded) - this is a violation
        if (firstArg is LiteralExpressionSyntax literal &&
            literal.Kind() == SyntaxKind.StringLiteralExpression)
        {
            return true;
        }

        // Interpolated strings might contain Messages.* or might be pure text.
        // Allow interpolated strings (they could use resource keys inside them)
        // If they're pure text interpolations, they'll be caught by other mechanisms.
        // For now, we allow them as they're harder to statically analyze.

        return false;
    }

    /// <summary>
    /// Creates a Violation record with relative path and line number from a syntax node.
    /// </summary>
    private static Violation CreateViolation(string repoRoot, SyntaxTree tree, SyntaxNode node)
    {
        var span = tree.GetLineSpan(node.Span);
        var line = span.StartLinePosition.Line + 1;
        var relativePath = Path.GetRelativePath(repoRoot, tree.FilePath);
        return new Violation(relativePath, line, node.ToString());
    }

    /// <summary>
    /// Record representing a single violation (hardcoded message in validator).
    /// </summary>
    private sealed record Violation(string File, int Line, string Expression)
    {
        public override string ToString() => $"{File}:{Line} -> {Expression}";
    }
}
