using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class CancellationTokenPropagationGuardTests
{
    [Test]
    public void Controllers_Should_Pass_RequestAborted_To_Service_Async_Calls()
    {
        var repoRoot = ResolveRepositoryRoot();
        var controllersRoot = Path.Combine(repoRoot, "LgymApi.Api", "Features");
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

        var controllerFiles = Directory
            .EnumerateFiles(controllersRoot, "*Controller.cs", SearchOption.AllDirectories)
            .ToList();

        Assert.That(controllerFiles, Is.Not.Empty, $"No controller files found in '{controllersRoot}'.");

        var violations = new List<Violation>();

        foreach (var file in controllerFiles)
        {
            var source = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(source, options: parseOptions, path: file);
            var root = tree.GetCompilationUnitRoot();

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!IsServiceAsyncCall(invocation))
                {
                    continue;
                }

                if (ContainsRequestAborted(invocation.ArgumentList))
                {
                    continue;
                }

                violations.Add(CreateViolation(repoRoot, tree, invocation, "Missing HttpContext.RequestAborted in service async call"));
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Controllers must pass HttpContext.RequestAborted to async service calls. Violations count: " + violations.Count + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    [Test]
    public void Services_Should_Expose_And_Propagate_CancellationToken()
    {
        var repoRoot = ResolveRepositoryRoot();
        var applicationRoot = Path.Combine(repoRoot, "LgymApi.Application");
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

        var serviceFiles = Directory
            .EnumerateFiles(applicationRoot, "*Service.cs", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith("EnumService.cs", StringComparison.Ordinal))
            .ToList();

        Assert.That(serviceFiles, Is.Not.Empty, $"No service files found in '{applicationRoot}'.");

        var violations = new List<Violation>();

        foreach (var file in serviceFiles)
        {
            var source = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(source, options: parseOptions, path: file);
            var root = tree.GetCompilationUnitRoot();

            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (!IsPublicAsyncContractMethod(method))
                {
                    continue;
                }

                if (!HasCancellationTokenParameter(method.ParameterList))
                {
                    violations.Add(CreateViolation(repoRoot, tree, method, "Missing CancellationToken parameter in async service method"));
                    continue;
                }

                foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (!RequiresCancellationTokenPropagation(invocation))
                    {
                        continue;
                    }

                    if (ContainsCancellationTokenArgument(invocation.ArgumentList))
                    {
                        continue;
                    }

                    violations.Add(CreateViolation(repoRoot, tree, invocation, "Missing cancellationToken propagation to repository/UoW async call"));
                }
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Async service methods must accept CancellationToken and propagate it to repositories/unit of work. Violations count: " + violations.Count + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    private static bool IsServiceAsyncCall(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        if (memberAccess.Expression is not IdentifierNameSyntax receiver)
        {
            return false;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        if (!methodName.EndsWith("Async", StringComparison.Ordinal))
        {
            return false;
        }

        var receiverName = receiver.Identifier.ValueText;
        return receiverName.StartsWith("_", StringComparison.Ordinal)
            && receiverName.EndsWith("Service", StringComparison.Ordinal);
    }

    private static bool ContainsRequestAborted(ArgumentListSyntax argumentList)
    {
        return argumentList.Arguments.Any(argument =>
            argument.Expression is MemberAccessExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.ValueText: "HttpContext" },
                Name: IdentifierNameSyntax { Identifier.ValueText: "RequestAborted" }
            });
    }

    private static bool IsPublicAsyncContractMethod(MethodDeclarationSyntax method)
    {
        if (!method.Modifiers.Any(SyntaxKind.PublicKeyword))
        {
            return false;
        }

        if (!method.Identifier.ValueText.EndsWith("Async", StringComparison.Ordinal))
        {
            return false;
        }

        return method.ReturnType is IdentifierNameSyntax { Identifier.ValueText: "Task" }
            || method.ReturnType is GenericNameSyntax { Identifier.ValueText: "Task" };
    }

    private static bool HasCancellationTokenParameter(ParameterListSyntax parameterList)
    {
        return parameterList.Parameters.Any(parameter =>
            parameter.Type is IdentifierNameSyntax { Identifier.ValueText: "CancellationToken" }
            && parameter.Identifier.ValueText == "cancellationToken");
    }

    private static bool RequiresCancellationTokenPropagation(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        if (!methodName.EndsWith("Async", StringComparison.Ordinal))
        {
            return false;
        }

        if (memberAccess.Expression is IdentifierNameSyntax receiver)
        {
            var receiverName = receiver.Identifier.ValueText;
            if (receiverName == "_unitOfWork" || receiverName == "transaction")
            {
                return true;
            }

            if (receiverName.StartsWith("_", StringComparison.Ordinal) && receiverName.EndsWith("Repository", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsCancellationTokenArgument(ArgumentListSyntax argumentList)
    {
        return argumentList.Arguments.Any(argument =>
            argument.Expression is IdentifierNameSyntax { Identifier.ValueText: "cancellationToken" }
            || argument.Expression.ToString().Contains("cancellationToken", StringComparison.Ordinal));
    }

    private static Violation CreateViolation(string repoRoot, SyntaxTree tree, SyntaxNode node, string reason)
    {
        var span = tree.GetLineSpan(node.Span);
        var line = span.StartLinePosition.Line + 1;
        var relativePath = Path.GetRelativePath(repoRoot, tree.FilePath);
        return new Violation(relativePath, line, reason);
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

    private sealed record Violation(string File, int Line, string Reason)
    {
        public override string ToString() => $"{File}:{Line} [{Reason}]";
    }
}
