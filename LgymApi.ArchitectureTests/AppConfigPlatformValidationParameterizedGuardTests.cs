using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class AppConfigPlatformValidationParameterizedGuardTests
{
    private static readonly string[] LegacyGetMethodNames =
    [
        "GetAppVersion_WithInvalidPlatform_ReturnsBadRequest",
        "GetAppVersion_WithMissingPlatform_ReturnsBadRequest",
        "GetAppVersion_WithNumericPlatform_ReturnsBadRequest"
    ];

    private static readonly string[] LegacyCreateMethodNames =
    [
        "CreateNewAppVersion_WithInvalidPlatform_ReturnsBadRequest",
        "CreateNewAppVersion_WithMissingPlatform_ReturnsBadRequest",
        "CreateNewAppVersion_WithNumericPlatform_ReturnsBadRequest"
    ];

    [Test]
    public void AppConfigPlatformValidation_Should_UseParameterizedTestCases()
    {
        var repoRoot = ResolveRepositoryRoot();
        var filePath = Path.Combine(repoRoot, "LgymApi.IntegrationTests", "AppConfigTests.cs");

        Assert.That(File.Exists(filePath), Is.True, $"Required test file '{filePath}' was not found.");

        var syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath), path: filePath);
        var root = syntaxTree.GetCompilationUnitRoot();

        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
        var allLegacyNames = LegacyGetMethodNames.Concat(LegacyCreateMethodNames).ToArray();

        // Guard 1: No legacy duplicated methods exist as standalone methods
        var legacyMethods = methods
            .Where(method => allLegacyNames.Contains(method.Identifier.Text, StringComparer.Ordinal))
            .ToList();

        Assert.That(
            legacyMethods,
            Is.Empty,
            "Duplicated AppConfig platform validation methods were reintroduced. Use parameterized tests with [TestCaseSource].");

        // Guard 2: Parameterized test methods exist
        var getMethod = methods.FirstOrDefault(method => method.Identifier.Text == "GetAppVersion_WithInvalidPlatformData_ReturnsBadRequest");
        Assert.That(
            getMethod,
            Is.Not.Null,
            "Expected parameterized method 'GetAppVersion_WithInvalidPlatformData_ReturnsBadRequest' was not found.");

        var createMethod = methods.FirstOrDefault(method => method.Identifier.Text == "CreateNewAppVersion_WithInvalidPlatformData_ReturnsBadRequest");
        Assert.That(
            createMethod,
            Is.Not.Null,
            "Expected parameterized method 'CreateNewAppVersion_WithInvalidPlatformData_ReturnsBadRequest' was not found.");

        // Guard 3: TestCaseSource methods exist and contain SetName calls covering all legacy scenarios
        var allMethodsAndMembers = root.DescendantNodes();

        var getSourceMethod = methods.FirstOrDefault(method => method.Identifier.Text == "GetAppVersion_InvalidPlatformCases");
        Assert.That(
            getSourceMethod,
            Is.Not.Null,
            "Expected TestCaseSource method 'GetAppVersion_InvalidPlatformCases' was not found.");

        var getSetNames = ExtractSetNameValues(getSourceMethod!);
        Assert.That(
            getSetNames,
            Is.SupersetOf(LegacyGetMethodNames),
            "The GetAppVersion TestCaseSource must preserve all original GET scenarios via .SetName().");

        var createSourceMethod = methods.FirstOrDefault(method => method.Identifier.Text == "CreateNewAppVersion_InvalidPlatformCases");
        Assert.That(
            createSourceMethod,
            Is.Not.Null,
            "Expected TestCaseSource method 'CreateNewAppVersion_InvalidPlatformCases' was not found.");

        var createSetNames = ExtractSetNameValues(createSourceMethod!);
        Assert.That(
            createSetNames,
            Is.SupersetOf(LegacyCreateMethodNames),
            "The CreateNewAppVersion TestCaseSource must preserve all original CREATE scenarios via .SetName().");
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
