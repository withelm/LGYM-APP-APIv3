using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class NoDependencyAggregateAbuseGuardTests
{
    private static readonly string[] AggregateSuffixes =
    [
        "Dependencies",
        "DependencyAggregate",
        "DependencyBag"
    ];

    [Test]
    public void Application_Service_Dependency_Aggregates_Should_Be_Owner_Aligned_And_Single_Service_Scoped()
    {
        var (repoRoot, compilation, syntaxTrees) = ArchitectureTestHelpers.PrepareCompilation("LgymApi.Application");

        var violations = Analyze(compilation, syntaxTrees, repoRoot);

        Assert.That(
            violations,
            Is.Empty,
            "Dependency aggregates may only be used for owner-aligned service composition. Shared or misaligned dependency bags must be removed or replaced with a legitimate service-owned aggregate." + Environment.NewLine +
            string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    [Test]
    public void Owner_Aligned_Dependency_Aggregate_Is_Allowed()
    {
        const string source = """
            namespace Example;

            public interface IUserServiceDependencies
            {
                IRepository Repo { get; }
            }

            public interface IRepository
            {
            }

            public sealed class UserService
            {
                public UserService(IUserServiceDependencies dependencies)
                {
                }
            }
            """;

        var violations = AnalyzeSource(source);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void Misaligned_Dependency_Aggregate_Is_Rejected_With_Service_Aggregate_And_Reason()
    {
        const string source = """
            namespace Example;

            public interface ISharedDependencies
            {
                IRepository Repo { get; }
            }

            public interface IRepository
            {
            }

            public sealed class UserService
            {
                public UserService(ISharedDependencies dependencies)
                {
                }
            }
            """;

        var violations = AnalyzeSource(source);

        Assert.Multiple(() =>
        {
            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].ServiceName, Is.EqualTo("UserService"));
            Assert.That(violations[0].AggregateTypeName, Is.EqualTo("ISharedDependencies"));
            Assert.That(violations[0].Reason, Does.Contain("not owner-aligned"));
            Assert.That(violations[0].ToString(), Does.Contain("UserService"));
            Assert.That(violations[0].ToString(), Does.Contain("ISharedDependencies"));
        });
    }

    [Test]
    public void Shared_Dependency_Aggregate_Is_Rejected_With_Sharing_Reason()
    {
        const string source = """
            namespace Example;

            public interface ISharedServiceDependencies
            {
                IRepository Repo { get; }
            }

            public interface IRepository
            {
            }

            public sealed class SharedService
            {
                public SharedService(ISharedServiceDependencies dependencies)
                {
                }
            }

            public sealed class SharedCloneService
            {
                public SharedCloneService(ISharedServiceDependencies dependencies)
                {
                }
            }
            """;

        var violations = AnalyzeSource(source);

        Assert.Multiple(() =>
        {
            Assert.That(violations, Has.Count.EqualTo(2));
            Assert.That(violations.All(v => v.Reason.Contains("consumed by multiple services", StringComparison.Ordinal)), Is.True);
            Assert.That(violations.Select(v => v.ServiceName), Is.EquivalentTo(["SharedService", "SharedCloneService"]));
            Assert.That(violations.All(v => v.AggregateTypeName == "ISharedServiceDependencies"), Is.True);
        });
    }

    private static List<Violation> AnalyzeSource(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Latest),
            path: "TestSource.cs");

        var compilation = ArchitectureTestHelpers.CreateCompilation([tree]);
        return Analyze(compilation, [tree], repoRoot: string.Empty);
    }

    private static List<Violation> Analyze(CSharpCompilation compilation, IReadOnlyList<SyntaxTree> syntaxTrees, string repoRoot)
    {
        var usages = new List<AggregateUsage>();

        foreach (var tree in syntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetCompilationUnitRoot();

            foreach (var serviceClass in root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(IsConcreteService))
            {
                var serviceSymbol = semanticModel.GetDeclaredSymbol(serviceClass);
                if (serviceSymbol == null)
                {
                    continue;
                }

                var constructors = serviceClass.Members.OfType<ConstructorDeclarationSyntax>().ToList();
                foreach (var constructor in constructors)
                {
                    var aggregateParameters = constructor.ParameterList.Parameters
                        .Select(parameter => CreateAggregateUsage(parameter, semanticModel, serviceSymbol, repoRoot, tree))
                        .Where(usage => usage != null)
                        .Cast<AggregateUsage>()
                        .ToList();

                    if (aggregateParameters.Count == 0)
                    {
                        continue;
                    }

                    foreach (var usage in aggregateParameters)
                    {
                        usages.Add(usage with { ConstructorAggregateCount = aggregateParameters.Count });
                    }
                }
            }
        }

        var consumersByAggregate = usages
            .GroupBy(usage => usage.AggregateIdentity, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(usage => usage.ServiceName)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal);

        return usages
            .Select(usage => CreateViolation(usage, consumersByAggregate[usage.AggregateIdentity]))
            .Where(violation => violation != null)
            .Cast<Violation>()
            .OrderBy(violation => violation.File, StringComparer.Ordinal)
            .ThenBy(violation => violation.Line)
            .ThenBy(violation => violation.ServiceName, StringComparer.Ordinal)
            .ToList();
    }

    private static AggregateUsage? CreateAggregateUsage(
        ParameterSyntax parameter,
        SemanticModel semanticModel,
        INamedTypeSymbol serviceSymbol,
        string repoRoot,
        SyntaxTree tree)
    {
        if (parameter.Type == null)
        {
            return null;
        }

        var typeSymbol = semanticModel.GetTypeInfo(parameter.Type).Type as INamedTypeSymbol;
        if (typeSymbol == null || !IsDependencyAggregateType(typeSymbol))
        {
            return null;
        }

        var lineSpan = tree.GetLineSpan(parameter.Span);
        var filePath = string.IsNullOrWhiteSpace(repoRoot)
            ? tree.FilePath
            : Path.GetRelativePath(repoRoot, tree.FilePath);

        return new AggregateUsage(
            filePath,
            lineSpan.StartLinePosition.Line + 1,
            serviceSymbol.Name,
            typeSymbol.Name,
            typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            GetAggregateOwnerServiceName(typeSymbol),
            ConstructorAggregateCount: 0);
    }

    private static Violation? CreateViolation(AggregateUsage usage, IReadOnlyList<string> consumers)
    {
        var reasons = new List<string>();

        if (!string.Equals(usage.OwnerAlignedServiceName, usage.ServiceName, StringComparison.Ordinal))
        {
            reasons.Add($"aggregate is not owner-aligned (expected '{usage.ServiceName}', found '{usage.OwnerAlignedServiceName}')");
        }

        if (consumers.Count > 1)
        {
            reasons.Add($"aggregate is consumed by multiple services: {string.Join(", ", consumers)}");
        }

        if (usage.ConstructorAggregateCount > 1)
        {
            reasons.Add($"constructor accepts {usage.ConstructorAggregateCount} dependency aggregates");
        }

        if (reasons.Count == 0)
        {
            return null;
        }

        return new Violation(
            usage.File,
            usage.Line,
            usage.ServiceName,
            usage.AggregateTypeName,
            string.Join("; ", reasons));
    }

    private static bool IsConcreteService(ClassDeclarationSyntax typeDeclaration)
    {
        return typeDeclaration.Identifier.ValueText.EndsWith("Service", StringComparison.Ordinal)
            && !typeDeclaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.AbstractKeyword));
    }

    private static bool IsDependencyAggregateType(INamedTypeSymbol typeSymbol)
    {
        var typeName = typeSymbol.Name;
        return AggregateSuffixes.Any(suffix => typeName.EndsWith(suffix, StringComparison.Ordinal));
    }

    private static string GetAggregateOwnerServiceName(INamedTypeSymbol typeSymbol)
    {
        var typeName = typeSymbol.Name;
        if (typeName.StartsWith("I", StringComparison.Ordinal) && typeSymbol.TypeKind == TypeKind.Interface && typeName.Length > 1)
        {
            typeName = typeName[1..];
        }

        foreach (var suffix in AggregateSuffixes)
        {
            if (typeName.EndsWith(suffix, StringComparison.Ordinal))
            {
                return typeName[..^suffix.Length];
            }
        }

        return typeName;
    }

    private sealed record AggregateUsage(
        string File,
        int Line,
        string ServiceName,
        string AggregateTypeName,
        string AggregateIdentity,
        string OwnerAlignedServiceName,
        int ConstructorAggregateCount);

    private sealed record Violation(
        string File,
        int Line,
        string ServiceName,
        string AggregateTypeName,
        string Reason)
    {
        public override string ToString()
            => $"{File}:{Line} -> {ServiceName} uses dependency aggregate {AggregateTypeName}: {Reason}";
    }
}
