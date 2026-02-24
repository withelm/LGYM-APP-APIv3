using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class ControllerDependencyInjectionGuardTests
{
    [Test]
    public void Controllers_Should_Resolve_Services_From_DI_Not_Construct_Implementations()
    {
        var repoRoot = ResolveRepositoryRoot();
        var controllersRoot = Path.Combine(repoRoot, "LgymApi.Api", "Features");

        Assert.That(Directory.Exists(controllersRoot), Is.True, $"Controllers root '{controllersRoot}' not found.");

        var controllerFiles = Directory
            .EnumerateFiles(controllersRoot, "*Controller.cs", SearchOption.AllDirectories)
            .ToList();

        Assert.That(controllerFiles, Is.Not.Empty, "No controllers discovered for DI guard test.");

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var violations = new List<Violation>();

        foreach (var file in controllerFiles)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), parseOptions, file);
            var root = tree.GetCompilationUnitRoot();

            var ctorDeclarations = root.DescendantNodes().OfType<ConstructorDeclarationSyntax>();

            foreach (var ctor in ctorDeclarations)
            {
                foreach (var statement in ctor.Body?.Statements.OfType<ExpressionStatementSyntax>() ?? Enumerable.Empty<ExpressionStatementSyntax>())
                {
                    if (statement.Expression is not AssignmentExpressionSyntax assignment)
                    {
                        continue;
                    }

                    if (assignment.Right is not ObjectCreationExpressionSyntax objectCreation)
                    {
                        continue;
                    }

                    var typeName = objectCreation.Type.ToString();
                    if (!typeName.EndsWith("Service", StringComparison.Ordinal) && !typeName.EndsWith("Repository", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    violations.Add(CreateViolation(repoRoot, tree, assignment, typeName));
                }
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Controllers must receive services/repositories via DI. Instantiations detected:" + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    private static Violation CreateViolation(string repoRoot, SyntaxTree tree, SyntaxNode node, string type)
    {
        var span = tree.GetLineSpan(node.Span);
        var line = span.StartLinePosition.Line + 1;
        var relativePath = Path.GetRelativePath(repoRoot, tree.FilePath);
        return new Violation(relativePath, line, type);
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

    private sealed record Violation(string File, int Line, string Type)
    {
        public override string ToString() => $"{File}:{Line} -> {Type}";
    }
}
