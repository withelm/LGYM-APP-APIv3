using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.AspNetCore.Mvc;
using LgymApi.Api.Interfaces;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class ControllerProducesResponseTypeDtoGuardTests
{
    [Test]
    public void ProducesResponseType_ShouldUseDtoTypesImplementingIDto()
    {
        var repoRoot = ResolveRepositoryRoot();
        var apiRoot = Path.Combine(repoRoot, "LgymApi.Api");
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

        Assert.That(Directory.Exists(apiRoot), Is.True, $"Api project directory '{apiRoot}' does not exist.");

        var sourceFiles = Directory
            .EnumerateFiles(apiRoot, "*.cs", SearchOption.AllDirectories)
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
            "ControllerProducesResponseTypeDtoGuard",
            syntaxTrees,
            ResolveMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var controllerBaseSymbol = compilation.GetTypeByMetadataName(typeof(ControllerBase).FullName!);
        var producesResponseTypeAttributeSymbol = compilation.GetTypeByMetadataName(typeof(ProducesResponseTypeAttribute).FullName!);
        var idtoSymbol = compilation.GetTypeByMetadataName(typeof(IDto).FullName!);

        Assert.That(controllerBaseSymbol, Is.Not.Null, "Unable to resolve ControllerBase symbol.");
        Assert.That(producesResponseTypeAttributeSymbol, Is.Not.Null, "Unable to resolve ProducesResponseTypeAttribute symbol.");
        Assert.That(idtoSymbol, Is.Not.Null, "Unable to resolve IDto symbol.");

        var violations = new List<Violation>();
        var producesAttributesFound = 0;
        var projectClassTypesFound = 0;

        foreach (var tree in syntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            var root = tree.GetCompilationUnitRoot();

            foreach (var methodDeclaration in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration);
                if (methodSymbol?.ContainingType == null)
                {
                    continue;
                }

                if (!InheritsFromControllerBase(methodSymbol.ContainingType, controllerBaseSymbol!))
                {
                    continue;
                }

                foreach (var attributeSyntax in methodDeclaration.AttributeLists.SelectMany(list => list.Attributes))
                {
                    var symbolInfo = semanticModel.GetSymbolInfo(attributeSyntax);
                    var attributeConstructor = symbolInfo.Symbol as IMethodSymbol
                        ?? symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();

                    if (attributeConstructor == null)
                    {
                        continue;
                    }

                    if (!SymbolEqualityComparer.Default.Equals(attributeConstructor.ContainingType, producesResponseTypeAttributeSymbol))
                    {
                        continue;
                    }

                    producesAttributesFound++;

                    var typeOfExpression = attributeSyntax.ArgumentList?.Arguments.FirstOrDefault()?.Expression as TypeOfExpressionSyntax;
                    if (typeOfExpression == null)
                    {
                        continue;
                    }

                    var responseTypeSymbol = semanticModel.GetTypeInfo(typeOfExpression.Type).Type;
                    if (responseTypeSymbol == null)
                    {
                        continue;
                    }

                    foreach (var classType in EnumerateClassTypes(responseTypeSymbol))
                    {
                        if (!IsProjectType(classType, compilation.Assembly))
                        {
                            continue;
                        }

                        projectClassTypesFound++;

                        if (classType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, idtoSymbol)))
                        {
                            continue;
                        }

                        var line = attributeSyntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        var filePath = attributeSyntax.SyntaxTree.FilePath;
                        var relativePath = Path.GetRelativePath(repoRoot, filePath);
                        violations.Add(new Violation(
                            relativePath,
                            line,
                            methodSymbol.ContainingType.Name,
                            methodSymbol.Name,
                            classType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                    }
                }
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Every class used in ProducesResponseType(typeof(...)) within ControllerBase descendants must implement IDto. Violations count: " +
            violations.Count + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));

        Assert.That(producesAttributesFound, Is.GreaterThan(0), "No ProducesResponseType attributes were analyzed.");
        Assert.That(
            projectClassTypesFound,
            Is.GreaterThan(0),
            "No project class types were found in ProducesResponseType(typeof(...)).");
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

    private static IEnumerable<INamedTypeSymbol> EnumerateClassTypes(ITypeSymbol typeSymbol)
    {
        switch (typeSymbol)
        {
            case IArrayTypeSymbol arrayType:
                foreach (var nested in EnumerateClassTypes(arrayType.ElementType))
                {
                    yield return nested;
                }

                yield break;

            case INamedTypeSymbol namedType:
                if (namedType.TypeKind == TypeKind.Class)
                {
                    yield return namedType;
                }

                foreach (var typeArgument in namedType.TypeArguments)
                {
                    foreach (var nested in EnumerateClassTypes(typeArgument))
                    {
                        yield return nested;
                    }
                }

                yield break;
        }
    }

    private static bool IsProjectType(INamedTypeSymbol typeSymbol, IAssemblySymbol projectAssembly)
    {
        return SymbolEqualityComparer.Default.Equals(typeSymbol.ContainingAssembly, projectAssembly);
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
            var apiProjectDir = Path.Combine(current.FullName, "LgymApi.Api");
            if (Directory.Exists(apiProjectDir))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    private sealed record Violation(string File, int Line, string Controller, string Method, string TypeName)
    {
        public override string ToString() => $"{File}:{Line} [{Controller}.{Method}] -> {TypeName}";
    }
}
