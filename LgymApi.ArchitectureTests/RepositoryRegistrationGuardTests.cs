using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class RepositoryRegistrationGuardTests
{
    private static readonly string[] ValidRegistrationMethods =
    {
        "AddScoped",
        "AddSingleton",
        "AddTransient"
    };

    [Test]
    public void Repositories_Should_Be_Registered_In_Infrastructure_ServiceCollection()
    {
        var repoRoot = ResolveRepositoryRoot();
        var repositoriesRoot = Path.Combine(repoRoot, "LgymApi.Infrastructure", "Repositories");
        var serviceExtensionsPath = Path.Combine(repoRoot, "LgymApi.Infrastructure", "ServiceCollectionExtensions.cs");

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(repositoriesRoot), Is.True, $"Repositories root '{repositoriesRoot}' not found.");
            Assert.That(File.Exists(serviceExtensionsPath), Is.True, $"Infrastructure ServiceCollectionExtensions file '{serviceExtensionsPath}' not found.");
        });

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var repositoryFiles = Directory
            .EnumerateFiles(repositoriesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsInBuildArtifacts(path))
            .ToList();

        Assert.That(repositoryFiles, Is.Not.Empty, "No repository files found for DI guard test.");

        var repositoryDeclarations = new List<RepositoryDeclaration>();
        foreach (var file in repositoryFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), parseOptions, file);
            var root = tree.GetCompilationUnitRoot();

            foreach (var typeDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!IsConcreteRepository(typeDeclaration))
                {
                    continue;
                }

                var interfaceName = ResolveRepositoryInterface(typeDeclaration);
                if (interfaceName == null)
                {
                    continue;
                }

                repositoryDeclarations.Add(new RepositoryDeclaration(interfaceName, typeDeclaration.Identifier.ValueText));
            }
        }

        Assert.That(repositoryDeclarations, Is.Not.Empty, "No concrete repositories detected.");

        var serviceExtensionsTree = CSharpSyntaxTree.ParseText(File.ReadAllText(serviceExtensionsPath), parseOptions, serviceExtensionsPath);
        var registrations = ExtractRegistrations(serviceExtensionsTree);

        var missing = repositoryDeclarations
            .Where(declaration => !registrations.Contains(declaration))
            .OrderBy(declaration => declaration.Interface, StringComparer.Ordinal)
            .ToList();

        Assert.That(
            missing,
            Is.Empty,
            "Every repository must be registered in Infrastructure ServiceCollectionExtensions." + Environment.NewLine +
            string.Join(Environment.NewLine, missing.Select(m => m.ToString())));
    }

    private static bool IsConcreteRepository(ClassDeclarationSyntax typeDeclaration)
    {
        if (!typeDeclaration.Identifier.ValueText.EndsWith("Repository", StringComparison.Ordinal))
        {
            return false;
        }

        return !typeDeclaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.AbstractKeyword));
    }

    private static string? ResolveRepositoryInterface(ClassDeclarationSyntax typeDeclaration)
    {
        if (typeDeclaration.BaseList == null)
        {
            return null;
        }

        foreach (var baseType in typeDeclaration.BaseList.Types.OfType<SimpleBaseTypeSyntax>())
        {
            var identifier = baseType.Type.ToString();
            if (identifier.StartsWith("I", StringComparison.Ordinal) && identifier.EndsWith("Repository", StringComparison.Ordinal))
            {
                return NormalizeType(baseType.Type);
            }
        }

        return null;
    }

    private static HashSet<RepositoryDeclaration> ExtractRegistrations(SyntaxTree serviceExtensionsTree)
    {
        var registrations = new HashSet<RepositoryDeclaration>();
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

            registrations.Add(new RepositoryDeclaration(interfaceType, implementationType));
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

    private sealed record RepositoryDeclaration(string Interface, string Implementation)
    {
        public override string ToString() => $"Missing repository registration: {Interface} -> {Implementation}";
    }
}
