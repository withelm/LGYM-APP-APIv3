using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class RegistrationValidationParameterizedGuardTests
{
    private static readonly string[] LegacyDuplicatedMethodNames =
    [
        "Register_WithEmptyName_ReturnsError",
        "Register_WithInvalidEmail_ReturnsError",
        "Register_WithShortPassword_ReturnsError",
        "Register_WithMismatchedPasswords_ReturnsError"
    ];

    [Test]
    public void UserAuthRegistrationValidation_Should_UseParameterizedTestCases()
    {
        var repoRoot = ResolveRepositoryRoot();
        var filePath = Path.Combine(repoRoot, "LgymApi.IntegrationTests", "UserAuthTests.cs");

        Assert.That(File.Exists(filePath), Is.True, $"Required test file '{filePath}' was not found.");

        var syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath), path: filePath);
        var root = syntaxTree.GetCompilationUnitRoot();

        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();

        var legacyMethods = methods
            .Where(method => LegacyDuplicatedMethodNames.Contains(method.Identifier.Text, StringComparer.Ordinal))
            .ToList();

        Assert.That(
            legacyMethods,
            Is.Empty,
            "Duplicated registration validation methods were reintroduced. Use a single parameterized test with [TestCase] attributes.");

        var parameterizedMethod = methods.FirstOrDefault(method => method.Identifier.Text == "Register_WithInvalidData_ReturnsBadRequest");

        Assert.That(
            parameterizedMethod,
            Is.Not.Null,
            "Expected parameterized method 'Register_WithInvalidData_ReturnsBadRequest' was not found.");

        var testCaseNames = parameterizedMethod!
            .AttributeLists
            .SelectMany(list => list.Attributes)
            .Where(attribute => IsTestCaseAttribute(attribute.Name.ToString()))
            .Select(attribute => attribute.ArgumentList?.Arguments
                .FirstOrDefault(arg => arg.NameEquals?.Name.Identifier.Text == "TestName")
                ?.Expression
            )
            .Select(ExtractTestName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.That(
            testCaseNames,
            Is.SupersetOf(LegacyDuplicatedMethodNames),
            "The parameterized registration validation test must preserve all original scenarios.");
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

    private static bool IsTestCaseAttribute(string attributeName)
    {
        return attributeName.EndsWith("TestCase", StringComparison.Ordinal)
            || attributeName.EndsWith("TestCaseAttribute", StringComparison.Ordinal);
    }

    private static string? ExtractTestName(ExpressionSyntax? expression)
    {
        if (expression is LiteralExpressionSyntax literal && literal.Kind() == SyntaxKind.StringLiteralExpression)
        {
            return literal.Token.ValueText;
        }

        if (expression is InvocationExpressionSyntax invocation
            && invocation.Expression is IdentifierNameSyntax identifier
            && identifier.Identifier.Text == "nameof"
            && invocation.ArgumentList.Arguments.Count == 1)
        {
            return invocation.ArgumentList.Arguments[0].Expression.ToString();
        }

        return expression?.ToString().Trim('"');
    }
}
