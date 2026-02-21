using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class ControllerDtoConstructionGuardTests
{
    [Test]
    public void Controllers_ShouldNotConstructDtosDirectly()
    {
        var repoRoot = ResolveRepositoryRoot();
        var controllersRoot = Path.Combine(repoRoot, "LgymApi.Api", "Features");
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var metadataReferences = ResolveMetadataReferences();

        Assert.That(
            Directory.Exists(controllersRoot),
            Is.True,
            $"Controllers root directory '{controllersRoot}' does not exist.");

        var controllerFiles = Directory
            .EnumerateFiles(controllersRoot, "*Controller.cs", SearchOption.AllDirectories)
            .ToList();

        Assert.That(
            controllerFiles,
            Is.Not.Empty,
            $"No controller files found in '{controllersRoot}'.");

        var violations = new List<Violation>();

        foreach (var file in controllerFiles)
        {
            var source = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(source, options: parseOptions, path: file);
            var parseErrors = tree
                .GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToList();

            Assert.That(
                parseErrors,
                Is.Empty,
                $"Failed to parse controller source file '{file}':{Environment.NewLine}{string.Join(Environment.NewLine, parseErrors)}");

            var root = tree.GetCompilationUnitRoot();
            var compilation = CSharpCompilation.Create(
                "ControllerDtoGuard",
                new[] { tree },
                metadataReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);

            foreach (var creation in root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                if (!IsInResponsePath(creation))
                {
                    continue;
                }

                var dtoType = TryGetDtoTypeName(creation, semanticModel);
                if (dtoType == null)
                {
                    continue;
                }

                violations.Add(CreateViolation(repoRoot, tree, creation, dtoType));
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Controllers must not construct DTOs directly. Violations count: " + violations.Count + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    private static Violation CreateViolation(string repoRoot, SyntaxTree tree, SyntaxNode node, string dtoType)
    {
        var span = tree.GetLineSpan(node.Span);
        var line = span.StartLinePosition.Line + 1;
        var relativePath = Path.GetRelativePath(repoRoot, tree.FilePath);
        return new Violation(relativePath, line, dtoType);
    }

    private static string? TryGetDtoTypeName(BaseObjectCreationExpressionSyntax creation, SemanticModel semanticModel)
    {
        var type = semanticModel.GetTypeInfo(creation).Type as INamedTypeSymbol;
        if (type == null)
        {
            return null;
        }

        return type.Name.EndsWith("Dto", StringComparison.Ordinal) ? type.Name : null;
    }

    private static bool IsInResponsePath(SyntaxNode node)
    {
        if (node.Ancestors().OfType<ReturnStatementSyntax>().Any())
        {
            return true;
        }

        if (node.Ancestors().OfType<ArrowExpressionClauseSyntax>().Any())
        {
            return true;
        }

        return false;
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

    private sealed record Violation(string File, int Line, string DtoType)
    {
        public override string ToString() => $"{File}:{Line} [{DtoType}]";
    }
}
