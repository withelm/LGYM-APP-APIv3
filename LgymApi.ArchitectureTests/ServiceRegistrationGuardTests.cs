using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class ServiceRegistrationGuardTests
{
    private static readonly string[] ValidRegistrationMethods =
    {
        "AddScoped",
        "AddSingleton",
        "AddTransient"
    };

    [Test]
    public void Feature_Services_Should_Be_Registered_In_ServiceCollection()
    {
        var repoRoot = ResolveRepositoryRoot();
        var featuresRoot = Path.Combine(repoRoot, "LgymApi.Application");
        var serviceExtensionsPath = Path.Combine(repoRoot, "LgymApi.Application", "ServiceCollectionExtensions.cs");

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(featuresRoot), Is.True, $"Application root '{featuresRoot}' not found.");
            Assert.That(File.Exists(serviceExtensionsPath), Is.True, $"ServiceCollectionExtensions file '{serviceExtensionsPath}' not found.");
        });

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var featureFiles = Directory
            .EnumerateFiles(featuresRoot, "*Service.cs", SearchOption.AllDirectories)
            .Where(path => !IsInBuildArtifacts(path) && IsFeatureServicePath(path))
            .ToList();

        Assert.That(featureFiles, Is.Not.Empty, "No feature files found for DI guard test.");

        var serviceDeclarations = new List<ServiceDeclaration>();
        foreach (var file in featureFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), parseOptions, file);
            var root = tree.GetCompilationUnitRoot();

            foreach (var typeDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!IsConcreteService(typeDeclaration))
                {
                    continue;
                }

                var interfaceName = ResolveServiceInterface(typeDeclaration);
                if (interfaceName == null)
                {
                    continue;
                }

                serviceDeclarations.Add(new ServiceDeclaration(interfaceName, typeDeclaration.Identifier.ValueText));
            }
        }

        Assert.That(serviceDeclarations, Is.Not.Empty, "No concrete feature services detected.");

        var serviceExtensionsTree = CSharpSyntaxTree.ParseText(File.ReadAllText(serviceExtensionsPath), parseOptions, serviceExtensionsPath);
        var registrations = ExtractRegistrations(serviceExtensionsTree);

        var missing = serviceDeclarations
            .Where(declaration => !registrations.Contains(declaration))
            .OrderBy(declaration => declaration.Interface, StringComparer.Ordinal)
            .ToList();

        Assert.That(
            missing,
            Is.Empty,
            "Every feature service must be registered in ServiceCollectionExtensions." + Environment.NewLine +
            string.Join(Environment.NewLine, missing.Select(m => m.ToString())));
    }

    private static bool IsConcreteService(ClassDeclarationSyntax typeDeclaration)
    {
        if (!typeDeclaration.Identifier.ValueText.EndsWith("Service", StringComparison.Ordinal))
        {
            return false;
        }

        return !typeDeclaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.AbstractKeyword));
    }

    private static bool IsFeatureServicePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (!normalized.Contains("/LgymApi.Application/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalized.Contains("/Services/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var excludedSegments = new[]
        {
            "/Repositories/",
            "/Units/",
            "/Mapping/",
            "/Models/",
            "/Exceptions/",
            "/Properties/"
        };

        return !excludedSegments.Any(segment => normalized.Contains(segment, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ResolveServiceInterface(ClassDeclarationSyntax typeDeclaration)
    {
        if (typeDeclaration.BaseList == null)
        {
            return null;
        }

        foreach (var baseType in typeDeclaration.BaseList.Types.OfType<SimpleBaseTypeSyntax>())
        {
            var identifier = baseType.Type.ToString();
            if (identifier.StartsWith('I') && identifier.EndsWith("Service", StringComparison.Ordinal))
            {
                return NormalizeType(baseType.Type);
            }
        }

        return null;
    }

    private static HashSet<ServiceDeclaration> ExtractRegistrations(SyntaxTree serviceExtensionsTree)
    {
        var registrations = new HashSet<ServiceDeclaration>();
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

            if (genericName.TypeArgumentList.Arguments.Count < 2)
            {
                continue;
            }

            var interfaceType = NormalizeType(genericName.TypeArgumentList.Arguments[0]);
            var implementationType = NormalizeType(genericName.TypeArgumentList.Arguments[1]);

            registrations.Add(new ServiceDeclaration(interfaceType, implementationType));
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

    private sealed record ServiceDeclaration(string Interface, string Implementation)
    {
        public override string ToString() => $"Missing registration: {Interface} -> {Implementation}";
    }
}
