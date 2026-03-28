using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class ApiContractTypedIdGuardTests
{
    [Test]
    public void Api_Contract_Dtos_Must_Not_Use_Typed_Id_ValueObject()
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
            "ApiContractTypedIdGuard",
            syntaxTrees,
            ResolveMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var typedIdSymbol = compilation.GetTypeByMetadataName("LgymApi.Domain.ValueObjects.Id`1");

        var violations = new List<Violation>();
        var contractTrees = syntaxTrees
            .Where(tree => IsContractsPath(tree.FilePath))
            .ToList();

        foreach (var tree in contractTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            var root = tree.GetCompilationUnitRoot();

            // Check property declarations
            foreach (var property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                if (IsTypedIdUsage(property.Type, semanticModel, typedIdSymbol))
                {
                    var line = property.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    var relativePath = Path.GetRelativePath(repoRoot, tree.FilePath);
                    violations.Add(new Violation(
                        relativePath,
                        line,
                        $"Property '{property.Identifier.ValueText}' uses Id<TEntity>"));
                }
            }

            // Check field declarations
            foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    if (IsTypedIdUsage(field.Declaration.Type, semanticModel, typedIdSymbol))
                    {
                        var line = field.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        var relativePath = Path.GetRelativePath(repoRoot, tree.FilePath);
                        violations.Add(new Violation(
                            relativePath,
                            line,
                            $"Field '{variable.Identifier.ValueText}' uses Id<TEntity>"));
                    }
                }
            }

            // Check method signatures (parameters and return types)
            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                // Check return type
                if (IsTypedIdUsage(method.ReturnType, semanticModel, typedIdSymbol))
                {
                    var line = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    var relativePath = Path.GetRelativePath(repoRoot, tree.FilePath);
                    violations.Add(new Violation(
                        relativePath,
                        line,
                        $"Method '{method.Identifier.ValueText}' return type uses Id<TEntity>"));
                }

                // Check parameters
                foreach (var parameter in method.ParameterList.Parameters)
                {
                    if (IsTypedIdUsage(parameter.Type, semanticModel, typedIdSymbol))
                    {
                        var line = parameter.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        var relativePath = Path.GetRelativePath(repoRoot, tree.FilePath);
                        violations.Add(new Violation(
                            relativePath,
                            line,
                            $"Method parameter '{parameter.Identifier.ValueText}' uses Id<TEntity>"));
                    }
                }
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "API contract DTOs must not use Id<TEntity>. DTO transport boundary must remain string-based." + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    private static bool IsTypedIdUsage(TypeSyntax? type, SemanticModel semanticModel, INamedTypeSymbol? typedIdSymbol)
    {
        if (type == null)
        {
            return false;
        }

        // Semantic detection - try to resolve the symbol
        var typeInfo = semanticModel.GetTypeInfo(type);
        if (typeInfo.Type != null && typedIdSymbol != null)
        {
            if (IsTypedIdSymbol(typeInfo.Type, typedIdSymbol))
            {
                return true;
            }
        }

        // Syntax fallback - check for generic name "Id<...>"
        if (type is GenericNameSyntax genericName)
        {
            if (genericName.Identifier.ValueText.Equals("Id", StringComparison.Ordinal))
            {
                return true;
            }
        }

        // Check for qualified generic names like ValueObjects.Id<T>
        if (type is QualifiedNameSyntax qualifiedName && qualifiedName.Right is GenericNameSyntax rightGeneric)
        {
            if (rightGeneric.Identifier.ValueText.Equals("Id", StringComparison.Ordinal))
            {
                return true;
            }
        }

        // Check for nullable types
        if (type is NullableTypeSyntax nullableType)
        {
            return IsTypedIdUsage(nullableType.ElementType, semanticModel, typedIdSymbol);
        }

        return false;
    }

    private static bool IsTypedIdSymbol(ITypeSymbol typeSymbol, INamedTypeSymbol typedIdSymbol)
    {
        if (typeSymbol is not INamedTypeSymbol namedType)
        {
            return false;
        }

        // Check if the original definition matches our typed ID symbol
        if (SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, typedIdSymbol))
        {
            return true;
        }

        return false;
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

    private sealed record Violation(string File, int Line, string Message)
    {
        public override string ToString() => $"{File}:{Line} {Message}";
    }
}
