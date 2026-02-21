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

            foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var dtoType = TryGetDtoTypeName(creation.Type);
                if (dtoType == null)
                {
                    continue;
                }

                violations.Add(CreateViolation(repoRoot, tree, creation, dtoType));
            }

            foreach (var creation in root.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>())
            {
                var dtoType = TryInferDtoTypeFromContext(creation);
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

    private static string? TryInferDtoTypeFromContext(ImplicitObjectCreationExpressionSyntax creation)
    {
        if (creation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } })
        {
            return TryGetDtoTypeName(declaration.Type);
        }

        if (creation.Parent is ReturnStatementSyntax)
        {
            var method = creation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (method != null)
            {
                return TryGetDtoTypeName(method.ReturnType);
            }

            var localFunction = creation.Ancestors().OfType<LocalFunctionStatementSyntax>().FirstOrDefault();
            if (localFunction != null)
            {
                return TryGetDtoTypeName(localFunction.ReturnType);
            }
        }

        if (creation.Parent is ArrowExpressionClauseSyntax)
        {
            var method = creation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (method != null)
            {
                return TryGetDtoTypeName(method.ReturnType);
            }

            var property = creation.Ancestors().OfType<PropertyDeclarationSyntax>().FirstOrDefault();
            if (property != null)
            {
                return TryGetDtoTypeName(property.Type);
            }
        }

        return null;
    }

    private static string? TryGetDtoTypeName(TypeSyntax type)
    {
        string typeName = type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.Text,
            NullableTypeSyntax nullable => TryGetDtoTypeName(nullable.ElementType) ?? string.Empty,
            _ => type.ToString().Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty
        };

        return typeName.EndsWith("Dto", StringComparison.Ordinal) ? typeName : null;
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
