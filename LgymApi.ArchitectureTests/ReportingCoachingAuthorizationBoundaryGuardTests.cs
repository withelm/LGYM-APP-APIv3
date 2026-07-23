using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.Features.Reporting;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class ReportingCoachingAuthorizationBoundaryGuardTests
{
    private const string AccessContractName = "LgymApi.Application.Coaching.Contracts.Access.ICoachingRelationshipAccessService";
    private const string LegacyRepositoryName = "ITrainerRelationshipRepository";

    private static readonly string[] AffectedTestFactories =
    [
        "LgymApi.UnitTests/PhotoServiceTestFactory.cs",
        "LgymApi.UnitTests/RecurringReportAssignmentServiceRelationalTests.cs",
        "LgymApi.UnitTests/RecurringReportAssignmentServiceTests.cs",
        "LgymApi.UnitTests/ReportingServiceAcceptedProgressOutboxTests.cs",
        "LgymApi.UnitTests/ReportingServiceTests.cs"
    ];

    [Test]
    public void CoachingRelationshipAccessContract_ShouldRemainIdOnly()
    {
        var method = typeof(ICoachingRelationshipAccessService).GetMethods().Single();

        Assert.Multiple(() =>
        {
            Assert.That(method.ReturnType, Is.EqualTo(typeof(Task<CoachingRelationshipAccessDecision>)));
            Assert.That(method.GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(new[]
            {
                typeof(Id<User>),
                typeof(Id<User>),
                typeof(CancellationToken)
            }));
            Assert.That(typeof(CoachingRelationshipAccessDecision).GetProperties().Select(property => property.PropertyType),
                Is.EqualTo(new[] { typeof(bool), typeof(bool) }));
        });
    }

    [TestCase(typeof(IReportingServiceDependencies))]
    [TestCase(typeof(IRecurringReportAssignmentServiceDependencies))]
    public void ReportingDependencyBag_ShouldExposeOnlyPublishedCoachingAccess(Type dependencyBag)
    {
        var coachingDependencies = dependencyBag.GetProperties()
            .Select(property => property.PropertyType)
            .Where(type => type.Namespace?.StartsWith("LgymApi.Application.Coaching", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.That(coachingDependencies, Is.EqualTo(new[] { typeof(ICoachingRelationshipAccessService) }));
        Assert.That(dependencyBag.GetProperties().Select(property => property.PropertyType.Name),
            Does.Not.Contain(LegacyRepositoryName));
    }

    [Test]
    public void ReportingProduction_ShouldUseOnlyPublishedCoachingAccess()
    {
        var (repoRoot, compilation, syntaxTrees) = ArchitectureTestHelpers.PrepareCompilation("LgymApi.Application");
        var reportingTrees = syntaxTrees
            .Where(tree => ArchitectureTestHelpers.NormalizePath(Path.GetRelativePath(repoRoot, tree.FilePath))
                .StartsWith("LgymApi.Application/Features/Reporting/", StringComparison.Ordinal))
            .ToArray();

        var coachingDependencies = reportingTrees
            .SelectMany(tree =>
            {
                var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
                return tree.GetRoot().DescendantNodes().OfType<TypeSyntax>()
                    .Select(type => semanticModel.GetTypeInfo(type).Type)
                    .Where(type => type?.ContainingNamespace.ToDisplayString()
                        .StartsWith("LgymApi.Application.Coaching", StringComparison.Ordinal) == true)
                    .Select(type => type!.ToDisplayString());
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var legacyReferences = reportingTrees
            .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>())
            .Where(identifier => identifier.Identifier.ValueText == LegacyRepositoryName)
            .Select(identifier => ArchitectureTestHelpers.NormalizePath(Path.GetRelativePath(repoRoot, identifier.SyntaxTree.FilePath)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(coachingDependencies, Is.EqualTo(new[] { AccessContractName }));
            Assert.That(legacyReferences, Is.Empty);
        });
    }

    [Test]
    public void AffectedReportingTestFactories_ShouldUsePublishedAccessSubstitutes()
    {
        var repoRoot = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var legacyReferences = new List<string>();
        var accessContractFactories = new List<string>();

        foreach (var relativePath in AffectedTestFactories)
        {
            var path = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
            var identifiers = tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>()
                .Select(identifier => identifier.Identifier.ValueText)
                .ToArray();

            if (identifiers.Contains(LegacyRepositoryName, StringComparer.Ordinal))
            {
                legacyReferences.Add(relativePath);
            }

            if (identifiers.Contains(nameof(ICoachingRelationshipAccessService), StringComparer.Ordinal))
            {
                accessContractFactories.Add(relativePath);
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(legacyReferences, Is.Empty);
            Assert.That(accessContractFactories, Is.EquivalentTo(AffectedTestFactories));
        });
    }
}
