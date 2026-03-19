using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class SerializationOptionsGuardTests
{
    [Test]
    public void JsonSerializer_Must_Use_SharedSerializationOptions_In_Production_Paths()
    {
        var (repoRoot, compilation, syntaxTrees) = PrepareCompilation();

        var violations = new List<Violation>();

        foreach (var tree in syntaxTrees)
        {
            // Skip test files
            if (IsTestPath(tree.FilePath))
            {
                continue;
            }

            // Only check production paths in BackgroundWorker and Infrastructure
            if (!IsTargetedProductionPath(tree.FilePath))
            {
                continue;
            }

            var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            var root = tree.GetCompilationUnitRoot();

            // Find all invocation expressions
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var symbolInfo = semanticModel.GetSymbolInfo(invocation);
                var methodSymbol = symbolInfo.Symbol as IMethodSymbol;

                if (methodSymbol == null)
                {
                    continue;
                }

                // Check if this is a JsonSerializer.Serialize or JsonSerializer.Deserialize call
                if (!IsJsonSerializerMethod(methodSymbol))
                {
                    continue;
                }

                // Check if SharedSerializationOptions.Current is passed
                if (UsesSharedSerializationOptions(invocation, semanticModel))
                {
                    continue;
                }

                var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var relativePath = Path.GetRelativePath(repoRoot, tree.FilePath);
                var methodName = methodSymbol.Name;

                violations.Add(new Violation(
                    relativePath,
                    line,
                    $"JsonSerializer.{methodName} must use SharedSerializationOptions.Current"));
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "JsonSerializer.Serialize/Deserialize must use SharedSerializationOptions.Current in BackgroundWorker and Infrastructure production paths to ensure consistent cross-module contract serialization." + Environment.NewLine +
            "Violations found:" + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    private static bool IsJsonSerializerMethod(IMethodSymbol methodSymbol)
    {
        if (methodSymbol.ContainingType?.Name != "JsonSerializer")
        {
            return false;
        }

        if (methodSymbol.ContainingNamespace?.ToDisplayString() != "System.Text.Json")
        {
            return false;
        }

        return methodSymbol.Name is "Serialize" or "Deserialize";
    }

    private static bool UsesSharedSerializationOptions(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        var arguments = invocation.ArgumentList.Arguments;

        foreach (var argument in arguments)
        {
            // Check if this argument is a member access to SharedSerializationOptions.Current
            if (argument.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
                var symbol = symbolInfo.Symbol;

                if (symbol is IPropertySymbol propertySymbol)
                {
                    if (propertySymbol.Name == "Current" &&
                        propertySymbol.ContainingType?.Name == "SharedSerializationOptions")
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsTargetedProductionPath(string path)
    {
        var normalized = Normalize(path);
        return normalized.Contains("/LgymApi.BackgroundWorker/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/LgymApi.Infrastructure/", StringComparison.OrdinalIgnoreCase);
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

        Assert.That(sourceFiles, Is.Not.Empty, "No C# source files found for serialization options guard.");

        var syntaxTrees = sourceFiles
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), options: parseOptions, path: path))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "SerializationOptionsGuard",
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

    private sealed record Violation(string File, int Line, string Message)
    {
        public override string ToString() => $"{File}:{Line} {Message}";
    }
}
