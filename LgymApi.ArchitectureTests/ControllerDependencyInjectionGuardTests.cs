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

            var controllerClasses = root
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(cls => cls.Identifier.ValueText.EndsWith("Controller", StringComparison.Ordinal))
                .ToList();

            foreach (var controllerClass in controllerClasses)
            {
                foreach (var member in controllerClass.Members)
                {
                    switch (member)
                    {
                        case ConstructorDeclarationSyntax ctor:
                            InspectBody(repoRoot, tree, ctor.Body, ctor.ExpressionBody?.Expression, violations);
                            break;
                        case MethodDeclarationSyntax method:
                            InspectBody(repoRoot, tree, method.Body, method.ExpressionBody?.Expression, violations);
                            break;
                    }
                }
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Controllers must receive services/repositories via DI. Instantiations detected:" + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    private static void InspectBody(
        string repoRoot,
        SyntaxTree tree,
        BlockSyntax? body,
        ExpressionSyntax? expressionBody,
        ICollection<Violation> violations)
    {
        if (body != null)
        {
            InspectNode(repoRoot, tree, body, violations);
        }

        if (expressionBody != null)
        {
            InspectNode(repoRoot, tree, expressionBody, violations);
        }
    }

    private static void InspectNode(string repoRoot, SyntaxTree tree, SyntaxNode node, ICollection<Violation> violations)
    {
        foreach (var objectCreation in node.DescendantNodesAndSelf().OfType<ObjectCreationExpressionSyntax>())
        {
            var typeName = objectCreation.Type.ToString();
            if (!IsForbiddenType(typeName))
            {
                continue;
            }

            violations.Add(CreateViolation(repoRoot, tree, objectCreation, typeName));
        }
    }

    private static bool IsForbiddenType(string typeName)
    {
        return typeName.EndsWith("Service", StringComparison.Ordinal)
            || typeName.EndsWith("Repository", StringComparison.Ordinal);
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
