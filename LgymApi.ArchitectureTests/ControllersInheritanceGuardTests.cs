using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class ControllersInheritanceGuardTests
{
    [Test]
    public void Controllers_Should_Inherit_ControllerBase()
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
            "ControllersInheritanceGuard",
            syntaxTrees,
            ResolveMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var controllerBaseSymbol = compilation.GetTypeByMetadataName(typeof(ControllerBase).FullName!);

        Assert.That(controllerBaseSymbol, Is.Not.Null, "Unable to resolve ControllerBase symbol.");

        var controllerTrees = syntaxTrees.Where(tree => IsControllersPath(tree.FilePath)).ToList();
        Assert.That(controllerTrees, Is.Not.Empty, "No controller files found for analysis.");

        var violations = new List<Violation>();

        foreach (var tree in controllerTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            var root = tree.GetCompilationUnitRoot();

            foreach (var classDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var typeSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
                if (typeSymbol == null)
                {
                    continue;
                }

                if (!IsProjectType(typeSymbol, compilation.Assembly))
                {
                    continue;
                }

                if (InheritsFromControllerBase(typeSymbol, controllerBaseSymbol!))
                {
                    continue;
                }

                var line = classDeclaration.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var relativePath = Path.GetRelativePath(repoRoot, tree.FilePath);
                violations.Add(new Violation(relativePath, line, typeSymbol.Name));
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Every class declared under Features/*/Controllers must inherit ControllerBase." + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    private static bool InheritsFromControllerBase(INamedTypeSymbol type, INamedTypeSymbol controllerBaseSymbol)
    {
        var current = type;
        while (current.BaseType != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current.BaseType, controllerBaseSymbol))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static bool IsProjectType(INamedTypeSymbol typeSymbol, IAssemblySymbol projectAssembly)
    {
        return SymbolEqualityComparer.Default.Equals(typeSymbol.ContainingAssembly, projectAssembly);
    }

    private static bool IsControllersPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/Features/") && normalized.Contains("/Controllers/");
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

    private sealed record Violation(string File, int Line, string TypeName)
    {
        public override string ToString() => $"{File}:{Line} {TypeName} does not inherit ControllerBase";
    }
}
