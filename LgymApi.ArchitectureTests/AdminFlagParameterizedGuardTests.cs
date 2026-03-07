using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class AdminFlagParameterizedGuardTests
{
    private static readonly string[] LegacyDuplicatedMethodNames =
    [
        "IsAdmin_WithNonAdminUser_ReturnsFalse",
        "IsAdmin_WithNullAdminFlag_ReturnsFalse",
        "IsAdmin_WithNonExistentUser_ReturnsFalse",
        "IsAdmin_WithInvalidGuidFormat_ReturnsFalse"
    ];

    [Test]
    public void AdminFlagValidation_Should_UseParameterizedTestCases()
    {
        var repoRoot = ResolveRepositoryRoot();
        var filePath = Path.Combine(repoRoot, "LgymApi.IntegrationTests", "AdminFlagTests.cs");

        Assert.That(File.Exists(filePath), Is.True, $"Required test file '{filePath}' was not found.");

        var syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath), path: filePath);
        var root = syntaxTree.GetCompilationUnitRoot();

        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();

        // Guard 1: No legacy duplicated methods exist as standalone methods
        var legacyMethods = methods
            .Where(method => LegacyDuplicatedMethodNames.Contains(method.Identifier.Text, StringComparer.Ordinal))
            .ToList();

        Assert.That(
            legacyMethods,
            Is.Empty,
            "Duplicated AdminFlag IsAdmin methods were reintroduced. Use a single parameterized test with [TestCaseSource].");

        // Guard 2: Parameterized test method exists
        var parameterizedMethod = methods.FirstOrDefault(method => method.Identifier.Text == "IsAdmin_WithVariousNonAdminScenarios_ReturnsFalse");
        Assert.That(
            parameterizedMethod,
            Is.Not.Null,
            "Expected parameterized method 'IsAdmin_WithVariousNonAdminScenarios_ReturnsFalse' was not found.");

        // Guard 3: TestCaseSource method exists and contains SetName calls covering all legacy scenarios
        var sourceMethod = methods.FirstOrDefault(method => method.Identifier.Text == "IsAdmin_FalseResultCases");
        Assert.That(
            sourceMethod,
            Is.Not.Null,
            "Expected TestCaseSource method 'IsAdmin_FalseResultCases' was not found.");

        var setNames = ExtractSetNameValues(sourceMethod!);
        Assert.That(
            setNames,
            Is.SupersetOf(LegacyDuplicatedMethodNames),
            "The AdminFlag TestCaseSource must preserve all original IsAdmin false-returning scenarios via .SetName().");
    }

    private static HashSet<string> ExtractSetNameValues(MethodDeclarationSyntax method)
    {
        return method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Name.Identifier.Text == "SetName")
            .SelectMany(invocation => invocation.ArgumentList.Arguments)
            .Select(arg => arg.Expression)
            .OfType<LiteralExpressionSyntax>()
            .Where(literal => literal.Kind() == SyntaxKind.StringLiteralExpression)
            .Select(literal => literal.Token.ValueText)
            .ToHashSet(StringComparer.Ordinal);
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
}
