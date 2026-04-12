using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class MiddlewareRegistrationGuardTests
{
    private static readonly string[] RequiredMiddleware =
    {
        "ExceptionHandlingMiddleware",
        "UserContextMiddleware",
        "ApiIdempotencyMiddleware"
    };

    [Test]
    public void Program_Should_Register_Required_Middleware()
    {
        var repoRoot = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var programPath = Path.Combine(repoRoot, "LgymApi.Api", "Program.cs");

        Assert.That(File.Exists(programPath), Is.True, $"Program.cs not found at '{programPath}'");

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var programContent = File.ReadAllText(programPath);
        var tree = CSharpSyntaxTree.ParseText(programContent, parseOptions, programPath);
        var root = tree.GetCompilationUnitRoot();

        // Extract all UseMiddleware invocations and middleware class references
        var registeredMiddleware = ExtractRegisteredMiddleware(root);

        var missing = RequiredMiddleware
            .Where(middleware => !registeredMiddleware.Contains(middleware))
            .ToList();

        Assert.That(
            missing,
            Is.Empty,
            $"Program.cs must register the following middleware: {string.Join(", ", RequiredMiddleware)}. " +
            $"Missing: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// Extracts registered middleware class names from the Program.cs AST.
    /// Looks for UseMiddleware<T>() generic invocations and middleware class references.
    /// </summary>
    private static List<string> ExtractRegisteredMiddleware(CompilationUnitSyntax root)
    {
        var registered = new HashSet<string>(StringComparer.Ordinal);

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            // Look for UseMiddleware<T>() or app.UseMiddleware<T>() pattern
            if (invocation.Expression is GenericNameSyntax generic &&
                generic.Identifier.ValueText == "UseMiddleware" &&
                generic.TypeArgumentList?.Arguments.Count > 0)
            {
                // Handle UseMiddleware<T>() where T is a generic argument
                var typeArg = generic.TypeArgumentList.Arguments[0];
                var middlewareName = ExtractTypeName(typeArg);
                if (middlewareName != null)
                {
                    registered.Add(middlewareName);
                }
            }

            // Alternative pattern: app.UseMiddleware<T>()
            if (invocation.Expression is MemberAccessExpressionSyntax member &&
                member.Name is GenericNameSyntax memberGeneric &&
                memberGeneric.Identifier.ValueText == "UseMiddleware" &&
                memberGeneric.TypeArgumentList?.Arguments.Count > 0)
            {
                var typeArg = memberGeneric.TypeArgumentList.Arguments[0];
                var middlewareName = ExtractTypeName(typeArg);
                if (middlewareName != null)
                {
                    registered.Add(middlewareName);
                }
            }

            // Pattern: UseMiddleware(typeof(T))
            if (invocation.Expression is MemberAccessExpressionSyntax altMember &&
                altMember.Name.Identifier.ValueText == "UseMiddleware" &&
                invocation.ArgumentList?.Arguments.Count > 0)
            {
                var arg = invocation.ArgumentList.Arguments[0].Expression;
                if (arg is TypeOfExpressionSyntax typeOf)
                {
                    var middlewareName = ExtractTypeName(typeOf.Type);
                    if (middlewareName != null)
                    {
                        registered.Add(middlewareName);
                    }
                }
            }
        }

        return registered.ToList();
    }

    /// <summary>
    /// Extracts the simple class name from a type syntax, handling qualified names like
    /// LgymApi.Api.Middleware.UserContextMiddleware and simple names like ExceptionHandlingMiddleware.
    /// </summary>
    private static string? ExtractTypeName(TypeSyntax type)
    {
        return type switch
        {
            // Simple name: ExceptionHandlingMiddleware
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            // Qualified name: LgymApi.Api.Middleware.UserContextMiddleware
            QualifiedNameSyntax qualified => ExtractSimpleNameFromQualified(qualified),
            _ => null
        };
    }

    /// <summary>
    /// Extracts the rightmost simple name from a qualified name.
    /// Example: LgymApi.Api.Middleware.UserContextMiddleware -> UserContextMiddleware
    /// </summary>
    private static string? ExtractSimpleNameFromQualified(QualifiedNameSyntax qualified)
    {
        if (qualified.Right is IdentifierNameSyntax identifier)
        {
            return identifier.Identifier.ValueText;
        }

        return null;
    }
}
