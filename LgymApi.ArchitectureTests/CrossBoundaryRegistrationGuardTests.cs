using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class CrossBoundaryRegistrationGuardTests
{
    private static readonly string[] ValidRegistrationMethods =
    {
        "AddScoped",
        "AddSingleton",
        "AddTransient"
    };

    [Test]
    public void CrossBoundary_Registrations_Should_Not_Use_Application_Implementations_In_Infrastructure()
    {
        var repoRoot = ResolveRepositoryRoot();
        var infrastructureServiceExtensionsPath = Path.Combine(repoRoot, "LgymApi.Infrastructure", "ServiceCollectionExtensions.cs");

        Assert.That(
            File.Exists(infrastructureServiceExtensionsPath),
            Is.True,
            $"Infrastructure ServiceCollectionExtensions file '{infrastructureServiceExtensionsPath}' not found.");

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var serviceExtensionsTree = CSharpSyntaxTree.ParseText(
            File.ReadAllText(infrastructureServiceExtensionsPath),
            parseOptions,
            infrastructureServiceExtensionsPath);

        var violations = ExtractCrossBoundaryViolations(serviceExtensionsTree)
            .OrderBy(violation => violation.LineNumber)
            .ThenBy(violation => violation.InterfaceType, StringComparer.Ordinal)
            .ToList();

        Assert.That(
            violations,
            Is.Empty,
            "Infrastructure composition root must not register implementations from LgymApi.Application." + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(violation => violation.ToString())));
    }

    private static IEnumerable<CrossBoundaryViolation> ExtractCrossBoundaryViolations(SyntaxTree serviceExtensionsTree)
    {
        var root = serviceExtensionsTree.GetCompilationUnitRoot();

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax { Name: GenericNameSyntax genericName })
            {
                continue;
            }

            if (!ValidRegistrationMethods.Contains(genericName.Identifier.ValueText))
            {
                continue;
            }

            if (genericName.TypeArgumentList.Arguments.Count != 2)
            {
                continue;
            }

            var interfaceType = NormalizeType(genericName.TypeArgumentList.Arguments[0]);
            var implementationType = NormalizeType(genericName.TypeArgumentList.Arguments[1]);

            if (!implementationType.StartsWith("LgymApi.Application.", StringComparison.Ordinal))
            {
                continue;
            }

            var lineSpan = invocation.GetLocation().GetLineSpan();
            var lineNumber = lineSpan.StartLinePosition.Line + 1;

            yield return new CrossBoundaryViolation(
                serviceExtensionsTree.FilePath,
                lineNumber,
                interfaceType,
                implementationType);
        }
    }

    private static string NormalizeType(TypeSyntax typeSyntax)
    {
        return typeSyntax
            .ToString()
            .Replace("global::", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
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

    private sealed record CrossBoundaryViolation(string FilePath, int LineNumber, string InterfaceType, string ImplementationType)
    {
        public override string ToString()
            => $"Cross-boundary registration at {FilePath}:{LineNumber}: {InterfaceType} -> {ImplementationType}";
    }
}
