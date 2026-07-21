using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class ReportingProgressCompatibilityAdapterGuardTests
{
    private const string CompatibilityAdapterFileName = "ReportSubmissionMeasurementWriter.cs";
    private const string CompatibilityAdapterInterfaceFileName = "IReportSubmissionMeasurementWriter.cs";

    private static readonly IReadOnlySet<string> AllowedMeasurementUtilityMetadataNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "LgymApi.Application.Features.Measurements.MeasurementUnitResolver"
    };

    [Test]
    public void Reporting_Feature_Files_Should_Keep_Workout_Progress_Persistence_Behind_The_Compatibility_Adapter()
    {
        var (repoRoot, compilation, syntaxTrees) = ArchitectureTestHelpers.PrepareCompilation("LgymApi.Application");
        var reportingDirectory = Path.Combine(repoRoot, "LgymApi.Application", "Features", "Reporting");
        var reportingSourceFiles = EnumerateReportingSourceFiles(reportingDirectory);
        var reportingSourcePaths = reportingSourceFiles
            .Select(NormalizeFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var reportingSyntaxTrees = syntaxTrees
            .Where(tree => reportingSourcePaths.Contains(NormalizeFullPath(tree.FilePath)))
            .OrderBy(tree => NormalizeFullPath(tree.FilePath), StringComparer.Ordinal)
            .ToArray();
        var compatibilityAdapterPath = NormalizeFullPath(Path.Combine(reportingDirectory, CompatibilityAdapterFileName));

        Assert.Multiple(() =>
        {
            Assert.That(reportingSourceFiles, Is.Not.Empty);
            Assert.That(reportingSourceFiles.Select(NormalizeFullPath), Does.Contain(compatibilityAdapterPath));
            Assert.That(
                reportingSyntaxTrees.Select(tree => NormalizeFullPath(tree.FilePath)),
                Is.EqualTo(reportingSourceFiles.Select(NormalizeFullPath)));
        });

        var violations = CollectViolations(
            compilation,
            reportingSyntaxTrees.Where(tree => !NormalizeFullPath(tree.FilePath).Equals(compatibilityAdapterPath, StringComparison.OrdinalIgnoreCase)),
            repoRoot);

        Assert.That(violations, Is.Empty, BuildViolationMessage(violations));
    }

    [Test]
    public void Report_Submission_Measurement_Writer_Interface_Should_Remain_Type_Neutral()
    {
        var (repoRoot, compilation, syntaxTrees) = ArchitectureTestHelpers.PrepareCompilation("LgymApi.Application");
        var interfaceTree = syntaxTrees.Single(tree => Path.GetFileName(tree.FilePath)
            .Equals(CompatibilityAdapterInterfaceFileName, StringComparison.Ordinal));
        var violations = CollectViolations(compilation, [interfaceTree], repoRoot);

        Assert.That(
            violations,
            Is.Empty,
            "IReportSubmissionMeasurementWriter must not expose Measurement entities or Workout & Progress repositories. " +
            "#386 owns the future cutover from this temporary Reporting compatibility adapter.\n" +
            BuildViolationMessage(violations));
    }

    [Test]
    public void New_Reporting_Measurement_Repository_Dependency_Should_Be_Rejected()
    {
        var (repoRoot, compilation, _) = ArchitectureTestHelpers.PrepareCompilation("LgymApi.Application");
        var fixture = CSharpSyntaxTree.ParseText("""
            using LgymApi.Application.Repositories;

            namespace LgymApi.Application.Features.Reporting;

            public sealed class NewReportingMeasurementPersistenceDependency
            {
                public IMeasurementRepository MeasurementRepository { get; init; } = default!;
            }
            """, path: Path.Combine(repoRoot, "LgymApi.Application", "Features", "Reporting", "NewReportingMeasurementPersistenceDependency.cs"));
        var violations = CollectViolations(compilation.AddSyntaxTrees(fixture), [fixture], repoRoot);

        Assert.That(
            violations.Select(violation => violation.TargetMetadataName),
            Is.EquivalentTo(["LgymApi.Application.Repositories.IMeasurementRepository"]),
            BuildViolationMessage(violations));
    }

    private static IReadOnlyList<string> EnumerateReportingSourceFiles(string reportingDirectory)
    {
        return Directory
            .EnumerateFiles(reportingDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !ArchitectureTestHelpers.IsInBuildArtifacts(path))
            .OrderBy(NormalizeFullPath, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<Violation> CollectViolations(
        CSharpCompilation compilation,
        IEnumerable<SyntaxTree> syntaxTrees,
        string repoRoot)
    {
        var violations = new Dictionary<string, Violation>(StringComparer.Ordinal);

        foreach (var syntaxTree in syntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree, ignoreAccessibility: true);
            var root = syntaxTree.GetCompilationUnitRoot();
            foreach (var referencedType in EnumerateReferencedTypes(semanticModel, root))
            {
                if (!IsForbiddenWorkoutProgressDependency(referencedType))
                {
                    continue;
                }

                var violation = new Violation(
                    ArchitectureTestHelpers.NormalizePath(Path.GetRelativePath(repoRoot, syntaxTree.FilePath)),
                    GetMetadataName(referencedType));
                violations.TryAdd(violation.Identity, violation);
            }
        }

        return violations.Values.OrderBy(violation => violation.Identity, StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateReferencedTypes(SemanticModel semanticModel, CompilationUnitSyntax root)
    {
        var relevantNodes = root.DescendantNodes().OfType<TypeSyntax>().Cast<SyntaxNode>()
            .Concat(root.DescendantNodes().OfType<IdentifierNameSyntax>());

        foreach (var node in relevantNodes)
        {
            var symbol = semanticModel.GetSymbolInfo(node).Symbol;
            var referencedType = ArchitectureTestHelpers.GetOwnedNamedTypeSymbol(symbol)
                ?? ArchitectureTestHelpers.GetOwnedNamedTypeSymbol(semanticModel.GetTypeInfo(node).Type);
            if (referencedType != null)
            {
                yield return referencedType.OriginalDefinition;
            }
        }
    }

    private static bool IsForbiddenWorkoutProgressDependency(INamedTypeSymbol type)
    {
        if (ArchitectureTestHelpers.TryGetPersistedEntityOwner(type, out var owner) &&
            owner == ArchitectureTestHelpers.WorkoutProgressModuleName)
        {
            return true;
        }

        if (type.Name.EndsWith("Repository", StringComparison.Ordinal) &&
            ArchitectureTestHelpers.GetCanonicalModuleNameForSymbol(type) == ArchitectureTestHelpers.WorkoutProgressModuleName)
        {
            return true;
        }

        return type.ContainingNamespace.ToDisplayString() == "LgymApi.Application.Features.Measurements" &&
               !AllowedMeasurementUtilityMetadataNames.Contains(GetMetadataName(type));
    }

    private static string GetMetadataName(INamedTypeSymbol type)
    {
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty, StringComparison.Ordinal);
    }

    private static string NormalizeFullPath(string path)
    {
        return ArchitectureTestHelpers.NormalizePath(Path.GetFullPath(path));
    }

    private static string BuildViolationMessage(IReadOnlyList<Violation> violations)
    {
        var details = violations.Count == 0
            ? "No direct Workout & Progress persistence dependencies were found."
            : string.Join(Environment.NewLine, violations.Select(violation => violation.ToString()));

        return "Only ReportSubmissionMeasurementWriter.cs may use the temporary Reporting-to-Progress persistence compatibility path. " +
               "#386 owns the cutover; do not add Reporting dependencies on Measurement or other Workout & Progress persistence types.\n" +
               details;
    }

    private sealed record Violation(string SourceFile, string TargetMetadataName)
    {
        public string Identity => $"{SourceFile}|{TargetMetadataName}";

        public override string ToString() => $"{SourceFile} references {TargetMetadataName}";
    }
}
