using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using LgymApi.Api.Interfaces;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class FeatureLocationExclusivityGuardTests
{
    [Test]
    public void ContractDto_Types_Only_Appear_Under_Contracts()
    {
        var (repoRoot, compilation, syntaxTrees) = PrepareCompilation();
        var idtoSymbol = compilation.GetTypeByMetadataName(typeof(IDto).FullName!);
        var resultDtoSymbol = compilation.GetTypeByMetadataName(typeof(IResultDto).FullName!);

        Assert.Multiple(() =>
        {
            Assert.That(idtoSymbol, Is.Not.Null, "Unable to resolve IDto symbol.");
            Assert.That(resultDtoSymbol, Is.Not.Null, "Unable to resolve IResultDto symbol.");
        });

        var violations = new List<Violation>();

        foreach (var tree in syntaxTrees)
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

                if (!ImplementsDtoInterface(typeSymbol, idtoSymbol!, resultDtoSymbol!))
                {
                    continue;
                }

                var filePath = tree.FilePath;
                if (IsTestPath(filePath) || IsContractsPath(filePath))
                {
                    continue;
                }

                var line = typeDeclaration.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var relativePath = Path.GetRelativePath(repoRoot, filePath);
                violations.Add(new Violation(relativePath, line, typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Types implementing IDto or IResultDto must be contained in Features/*/Contracts folders only." + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    [Test]
    public void ControllerBase_Derived_Types_Only_Live_Under_Controllers()
    {
        var (repoRoot, compilation, syntaxTrees) = PrepareCompilation();
        var controllerBaseSymbol = compilation.GetTypeByMetadataName(typeof(ControllerBase).FullName!);

        Assert.That(controllerBaseSymbol, Is.Not.Null, "Unable to resolve ControllerBase symbol.");

        var violations = new List<Violation>();

        foreach (var tree in syntaxTrees)
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

                if (!InheritsFromControllerBase(typeSymbol, controllerBaseSymbol!))
                {
                    continue;
                }

                var filePath = tree.FilePath;
                if (IsTestPath(filePath) || IsControllersPath(filePath))
                {
                    continue;
                }

                var line = classDeclaration.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var relativePath = Path.GetRelativePath(repoRoot, filePath);
                violations.Add(new Violation(relativePath, line, typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Controllers inheriting ControllerBase must be declared only inside Features/*/Controllers folders." + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    [Test]
    public void FluentValidation_Validators_Only_Live_Under_Validation()
    {
        var (repoRoot, compilation, syntaxTrees) = PrepareCompilation();
        var abstractValidatorSymbol = compilation.GetTypeByMetadataName(typeof(AbstractValidator<>).FullName!);

        Assert.That(abstractValidatorSymbol, Is.Not.Null, "Unable to resolve AbstractValidator<T> symbol.");

        var violations = new List<Violation>();

        foreach (var tree in syntaxTrees)
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

                if (!InheritsFromAbstractValidator(typeSymbol, abstractValidatorSymbol!))
                {
                    continue;
                }

                var filePath = tree.FilePath;
                if (IsTestPath(filePath) || IsValidationPath(filePath))
                {
                    continue;
                }

                var line = classDeclaration.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var relativePath = Path.GetRelativePath(repoRoot, filePath);
                violations.Add(new Violation(relativePath, line, typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "FluentValidation validators inheriting AbstractValidator<T> must live only within Features/*/Validation folders." + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    private static (string RepoRoot, CSharpCompilation Compilation, IReadOnlyList<SyntaxTree> SyntaxTrees) PrepareCompilation()
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

        var compilation = CSharpCompilation.Create(
            "FeatureLocationExclusivityGuard",
            syntaxTrees,
            ResolveMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return (repoRoot, compilation, syntaxTrees);
    }

    private static bool ImplementsDtoInterface(INamedTypeSymbol typeSymbol, INamedTypeSymbol idtoSymbol, INamedTypeSymbol resultDtoSymbol)
    {
        if (typeSymbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, idtoSymbol)))
        {
            return true;
        }

        return typeSymbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, resultDtoSymbol));
    }

    private static bool InheritsFromControllerBase(INamedTypeSymbol typeSymbol, INamedTypeSymbol controllerBaseSymbol)
    {
        var current = typeSymbol;
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

    private static bool InheritsFromAbstractValidator(INamedTypeSymbol typeSymbol, INamedTypeSymbol abstractValidatorSymbol)
    {
        var current = typeSymbol.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, abstractValidatorSymbol))
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

    private static bool IsContractsPath(string path)
    {
        var normalized = Normalize(path);
        return normalized.Contains("/features/", StringComparison.OrdinalIgnoreCase)
            && normalized.Contains("/contracts/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsControllersPath(string path)
    {
        var normalized = Normalize(path);
        return normalized.Contains("/features/", StringComparison.OrdinalIgnoreCase)
            && normalized.Contains("/controllers/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidationPath(string path)
    {
        var normalized = Normalize(path);
        return normalized.Contains("/features/", StringComparison.OrdinalIgnoreCase)
            && normalized.Contains("/validation/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTestPath(string path)
    {
        var normalized = Normalize(path);
        return normalized.Contains("/tests/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/unittests/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/architecturetests/", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path)
    {
        return path.Replace('\\', '/');
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
        var normalized = Normalize(path);
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record Violation(string File, int Line, string TypeName)
    {
        public override string ToString() => $"{File}:{Line} {TypeName}";
    }
}
