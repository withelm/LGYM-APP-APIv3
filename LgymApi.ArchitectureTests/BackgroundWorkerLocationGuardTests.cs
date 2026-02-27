using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class BackgroundWorkerLocationGuardTests
{
    [Test]
    public void BackgroundWorker_Implementations_Must_Live_In_BackgroundWorker_Project()
    {
        var (repoRoot, compilation, syntaxTrees) = PrepareCompilation();

        var violations = new List<Violation>();

        foreach (var tree in syntaxTrees)
        {
            if (IsTestPath(tree.FilePath))
            {
                continue;
            }

            var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            var root = tree.GetCompilationUnitRoot();
            var filePath = tree.FilePath;
            var isBackgroundWorkerFile = IsBackgroundWorkerPath(filePath);

            foreach (var typeSyntax in root.DescendantNodes().OfType<TypeSyntax>())
            {
                var resolvedType = semanticModel.GetTypeInfo(typeSyntax).Type;
                if (!IsBackgroundJobClientSymbol(resolvedType))
                {
                    continue;
                }

                if (isBackgroundWorkerFile)
                {
                    continue;
                }

                var line = typeSyntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var relativePath = Path.GetRelativePath(repoRoot, filePath);
                violations.Add(new Violation(relativePath, line, "IBackgroundJobClient type usage outside LgymApi.BackgroundWorker"));
            }

            foreach (var classDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var typeSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
                if (typeSymbol == null)
                {
                    continue;
                }

                if (!typeSymbol.AllInterfaces.Any(IsBackgroundJobClientSymbol))
                {
                    continue;
                }

                if (isBackgroundWorkerFile)
                {
                    continue;
                }

                var line = classDeclaration.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var relativePath = Path.GetRelativePath(repoRoot, filePath);
                violations.Add(new Violation(relativePath, line, $"Type {typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} implements IBackgroundJobClient outside LgymApi.BackgroundWorker"));
            }

            foreach (var constructor in root.DescendantNodes().OfType<ConstructorDeclarationSyntax>())
            {
                foreach (var parameter in constructor.ParameterList.Parameters)
                {
                    if (parameter.Type == null)
                    {
                        continue;
                    }

                    var parameterType = semanticModel.GetTypeInfo(parameter.Type).Type;
                    if (!IsBackgroundJobClientSymbol(parameterType))
                    {
                        continue;
                    }

                    if (isBackgroundWorkerFile)
                    {
                        continue;
                    }

                    var line = parameter.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    var relativePath = Path.GetRelativePath(repoRoot, filePath);
                    violations.Add(new Violation(relativePath, line, $"Constructor {constructor.Identifier.Text} injects IBackgroundJobClient outside LgymApi.BackgroundWorker"));
                }
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "IBackgroundJobClient usages, implementations, and constructor injections must be located only in LgymApi.BackgroundWorker project." + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    private static bool IsBackgroundJobClientSymbol(ITypeSymbol? symbol)
    {
        if (symbol == null)
        {
            return false;
        }

        return string.Equals(symbol.Name, "IBackgroundJobClient", StringComparison.Ordinal)
            && string.Equals(symbol.ContainingNamespace?.ToDisplayString(), "Hangfire", StringComparison.Ordinal);
    }

    private static bool IsBackgroundWorkerPath(string path)
    {
        var normalized = Normalize(path);
        return normalized.Contains("/LgymApi.BackgroundWorker/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTestPath(string path)
    {
        var normalized = Normalize(path);
        return normalized.Contains("/tests/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/unittests/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/integrationtests/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/architecturetests/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/lgymapi.unittests/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/lgymapi.integrationtests/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/lgymapi.architecturetests/", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static (string RepoRoot, CSharpCompilation Compilation, IReadOnlyList<SyntaxTree> SyntaxTrees) PrepareCompilation()
    {
        var repoRoot = ResolveRepositoryRoot();
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var sourceFiles = Directory
            .EnumerateFiles(repoRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsInBuildArtifacts(path))
            .ToList();

        Assert.That(sourceFiles, Is.Not.Empty, "No C# source files found for background worker location guard.");

        var syntaxTrees = sourceFiles
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), options: parseOptions, path: path))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "BackgroundWorkerLocationGuard",
            syntaxTrees,
            ResolveMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return (repoRoot, compilation, syntaxTrees);
    }

    private static bool IsInBuildArtifacts(string path)
    {
        var normalized = Normalize(path);
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
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

    private sealed record Violation(string File, int Line, string TypeName)
    {
        public override string ToString() => $"{File}:{Line} {TypeName}";
    }
}
