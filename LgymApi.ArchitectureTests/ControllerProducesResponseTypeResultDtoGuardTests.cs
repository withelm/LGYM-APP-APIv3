using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.AspNetCore.Mvc;
using LgymApi.Api.Interfaces;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class ControllerProducesResponseTypeResultDtoGuardTests
{
    [Test]
    public void ProducesResponseType_ShouldUseIResultDtoForResponses()
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
            "ControllerProducesResponseTypeResultDtoGuard",
            syntaxTrees,
            ResolveMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var controllerBaseSymbol = compilation.GetTypeByMetadataName(typeof(ControllerBase).FullName!);
        var producesResponseTypeAttributeSymbol = compilation.GetTypeByMetadataName(typeof(ProducesResponseTypeAttribute).FullName!);
        var resultDtoSymbol = compilation.GetTypeByMetadataName(typeof(IResultDto).FullName!);

        Assert.Multiple(() =>
        {
            Assert.That(controllerBaseSymbol, Is.Not.Null, "Unable to resolve ControllerBase symbol.");
            Assert.That(producesResponseTypeAttributeSymbol, Is.Not.Null, "Unable to resolve ProducesResponseTypeAttribute symbol.");
            Assert.That(resultDtoSymbol, Is.Not.Null, "Unable to resolve IResultDto symbol.");
        });

        var violations = new List<Violation>();
        var producesAttributesFound = 0;
        var projectResultTypesFound = 0;

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

                    foreach (var candidateType in EnumerateDtoCandidateTypes(responseTypeSymbol))
                    {
                        if (!IsProjectType(candidateType, compilation.Assembly))
                        {
                            continue;
                        }

                        projectResultTypesFound++;

                        var reasons = new List<string>();

                        if (!candidateType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, resultDtoSymbol)))
                        {
                            reasons.Add("does not implement IResultDto");
                        }

                        if (candidateType.TypeKind == TypeKind.Class && !candidateType.IsSealed)
                        {
                            reasons.Add("class/record is not sealed");
                        }

                        if (reasons.Count == 0)
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
                            candidateType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                            string.Join(", ", reasons)));
                    }
                }
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(
                violations,
                Is.Empty,
                "Every project type used in ProducesResponseType(typeof(...)) within ControllerBase descendants must implement IResultDto, and class/record types must be sealed. Violations count: " +
                violations.Count + Environment.NewLine +
                string.Join(Environment.NewLine, violations.Select(v => v.ToString())));

            Assert.That(producesAttributesFound, Is.GreaterThan(0), "No ProducesResponseType attributes were analyzed.");
            Assert.That(
                projectResultTypesFound,
                Is.GreaterThan(0),
                "No project result types were found in ProducesResponseType(typeof(...)).");
        });
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

    private static IEnumerable<INamedTypeSymbol> EnumerateDtoCandidateTypes(ITypeSymbol typeSymbol)
    {
        switch (typeSymbol)
        {
            case IArrayTypeSymbol arrayType:
                foreach (var nested in EnumerateDtoCandidateTypes(arrayType.ElementType))
                {
                    yield return nested;
                }

                yield break;

            case INamedTypeSymbol namedType:
                if (namedType.TypeKind is TypeKind.Class or TypeKind.Struct)
                {
                    yield return namedType;
                }

                foreach (var typeArgument in namedType.TypeArguments)
                {
                    foreach (var nested in EnumerateDtoCandidateTypes(typeArgument))
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

    private static bool IsInBuildArtifacts(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record Violation(string File, int Line, string Controller, string Method, string TypeName, string Reason)
    {
        public override string ToString() => $"{File}:{Line} [{Controller}.{Method}] -> {TypeName} ({Reason})";
    }
}
