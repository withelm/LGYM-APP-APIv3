using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class UnitConverterRegistrationGuardTests
{
    [Test]
    public void Unit_Strategies_And_Converters_Should_Be_Registered_In_ServiceCollection()
    {
        var repoRoot = ResolveRepositoryRoot();
        var unitsRoot = Path.Combine(repoRoot, "LgymApi.Application", "Units");
        var serviceExtensionsPath = Path.Combine(repoRoot, "LgymApi.Application", "ServiceCollectionExtensions.cs");

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(unitsRoot), Is.True, $"Units root '{unitsRoot}' not found.");
            Assert.That(File.Exists(serviceExtensionsPath), Is.True, $"ServiceCollectionExtensions file '{serviceExtensionsPath}' not found.");
        });

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var unitFiles = Directory
            .EnumerateFiles(unitsRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsInBuildArtifacts(path))
            .ToList();

        var requiredRegistrations = new List<UnitRegistration>();
        foreach (var file in unitFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), parseOptions, file);
            var root = tree.GetCompilationUnitRoot();

            foreach (var typeDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!typeDeclaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.AbstractKeyword))
                    && typeDeclaration.Identifier.ValueText.EndsWith("LinearUnitStrategy", StringComparison.Ordinal))
                {
                    requiredRegistrations.Add(new UnitRegistration(typeDeclaration.Identifier.ValueText, "LinearUnitStrategy"));
                }

                if (!typeDeclaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.AbstractKeyword))
                    && typeDeclaration.Identifier.ValueText.EndsWith("UnitConverter", StringComparison.Ordinal))
                {
                    requiredRegistrations.Add(new UnitRegistration(typeDeclaration.Identifier.ValueText, "UnitConverter"));
                }
            }
        }

        if (requiredRegistrations.Count == 0)
        {
            Assert.Pass("No unit strategies or converters detected – nothing to validate.");
            return;
        }

        var serviceExtensionsTree = CSharpSyntaxTree.ParseText(File.ReadAllText(serviceExtensionsPath), parseOptions, serviceExtensionsPath);
        var registrations = ExtractRegistrations(serviceExtensionsTree);

        var missing = requiredRegistrations
            .Where(registration => !registrations.Contains(registration))
            .OrderBy(registration => registration.TypeName, StringComparer.Ordinal)
            .ToList();

        Assert.That(
            missing,
            Is.Empty,
            "Unit strategies and converters must be registered in ServiceCollectionExtensions." + Environment.NewLine +
            string.Join(Environment.NewLine, missing.Select(m => m.ToString())));
    }

    private static HashSet<UnitRegistration> ExtractRegistrations(SyntaxTree serviceExtensionsTree)
    {
        var registrations = new HashSet<UnitRegistration>();
        var root = serviceExtensionsTree.GetCompilationUnitRoot();

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            if (memberAccess.Name is not GenericNameSyntax genericName)
            {
                continue;
            }

            if (genericName.TypeArgumentList.Arguments.Count < 2)
            {
                continue;
            }

            var implementationType = SimplifyTypeName(NormalizeType(genericName.TypeArgumentList.Arguments[1]));
            if (implementationType.EndsWith("LinearUnitStrategy", StringComparison.Ordinal))
            {
                registrations.Add(new UnitRegistration(implementationType, "LinearUnitStrategy"));
            }

            if (implementationType.EndsWith("UnitConverter", StringComparison.Ordinal))
            {
                registrations.Add(new UnitRegistration(implementationType, "UnitConverter"));
            }
        }

        return registrations;
    }

    private static string NormalizeType(TypeSyntax typeSyntax)
    {
        return typeSyntax
            .ToString()
            .Replace("global::", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    private static string SimplifyTypeName(string typeName)
    {
        var genericIndex = typeName.IndexOf('<');
        return genericIndex >= 0 ? typeName[..genericIndex] : typeName;
    }

    private static bool IsInBuildArtifacts(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase) || normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
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

    private sealed record UnitRegistration(string TypeName, string Kind)
    {
        public override string ToString() => $"Missing unit registration for {Kind}: {TypeName}";
    }
}
