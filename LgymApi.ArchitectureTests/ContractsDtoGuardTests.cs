using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using LgymApi.Api.Interfaces;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class ContractsDtoGuardTests
{
    [Test]
    public void Contracts_Should_Define_Only_IDto_Or_IResultDto()
    {
        var repoRoot = ResolveRepositoryRoot();
        var apiRoot = Path.Combine(repoRoot, "LgymApi.Api");
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

        Assert.That(Directory.Exists(apiRoot), Is.True, $"Api project directory '{apiRoot}' does not exist.");

        var sourceFiles = Directory
            .EnumerateFiles(apiRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsInBuildArtifacts(path))
            .ToList();

        Assert.That(sourceFiles, Is.Not.Empty, $"No source files found in '{apiRoot}'.");

        var syntaxTrees = sourceFiles
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), options: parseOptions, path: path))
            .ToList();

        foreach (var tree in syntaxTrees)
        {
            var parseErrors = tree
                .GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToList();

            Assert.That(
                parseErrors,
                Is.Empty,
                $"Failed to parse source file '{tree.FilePath}':{Environment.NewLine}{string.Join(Environment.NewLine, parseErrors)}");
        }

        var compilation = CSharpCompilation.Create(
            "ContractsDtoGuard",
            syntaxTrees,
            ResolveMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var idtoSymbol = compilation.GetTypeByMetadataName(typeof(IDto).FullName!);
        var resultDtoSymbol = compilation.GetTypeByMetadataName(typeof(IResultDto).FullName!);

        Assert.Multiple(() =>
        {
            Assert.That(idtoSymbol, Is.Not.Null, "Unable to resolve IDto symbol.");
            Assert.That(resultDtoSymbol, Is.Not.Null, "Unable to resolve IResultDto symbol.");
        });

        var violations = new List<Violation>();
        var contractTrees = syntaxTrees
            .Where(tree => IsContractsPath(tree.FilePath))
            .ToList();

        foreach (var tree in contractTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            var root = tree.GetCompilationUnitRoot();

            foreach (var typeDeclaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
                if (typeSymbol == null)
                {
                    continue;
                }

                if (!IsProjectType(typeSymbol, compilation.Assembly))
                {
                    continue;
                }

                if (typeSymbol.TypeKind is not TypeKind.Class and not TypeKind.Struct)
                {
                    continue;
                }

                if (ImplementsDtoInterface(typeSymbol, idtoSymbol!, resultDtoSymbol!))
                {
                    continue;
                }

                var line = typeDeclaration.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var relativePath = Path.GetRelativePath(repoRoot, tree.FilePath);
                violations.Add(new Violation(relativePath, line, typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Every type declared inside a Contracts folder must implement IDto or IResultDto." + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    private static bool ImplementsDtoInterface(INamedTypeSymbol typeSymbol, INamedTypeSymbol idtoSymbol, INamedTypeSymbol resultDtoSymbol)
    {
        if (typeSymbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, idtoSymbol)))
        {
            return true;
        }

        return typeSymbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, resultDtoSymbol));
    }

    private static bool IsProjectType(INamedTypeSymbol typeSymbol, IAssemblySymbol projectAssembly)
    {
        return SymbolEqualityComparer.Default.Equals(typeSymbol.ContainingAssembly, projectAssembly);
    }

    private static bool IsContractsPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/Contracts/", StringComparison.OrdinalIgnoreCase);
    }

    private static List<MetadataReference> ResolveMetadataReferences()
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .Select(assembly => assembly.Location)
            .Where(location => !string.IsNullOrWhiteSpace(location) && File.Exists(location))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(location => (MetadataReference)MetadataReference.CreateFromFile(location))
            .ToList();
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

    private static bool IsInBuildArtifacts(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record Violation(string File, int Line, string Type)
    {
        public override string ToString() => $"{File}:{Line} {Type} does not implement IDto or IResultDto";
    }
}
