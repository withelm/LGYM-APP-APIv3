using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class WorkoutProgressPublicContractGuardTests
{
    private static readonly IReadOnlySet<string> ForeignModuleNames = new HashSet<string>(StringComparer.Ordinal)
    {
        ArchitectureTestHelpers.IdentityModuleName,
        ArchitectureTestHelpers.CoachingModuleName,
        ArchitectureTestHelpers.NutritionModuleName,
        ArchitectureTestHelpers.NotificationsModuleName
    };

    private static readonly IReadOnlySet<string> AllowedPublicContractMetadataNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "LgymApi.Application.WorkoutProgress.ProgressData.IWorkoutProgressReadWriteService",
        "LgymApi.Application.WorkoutProgress.ProgressData.Models.ExerciseScoreChartPoint",
        "LgymApi.Application.WorkoutProgress.ProgressData.Models.MeasurementReadModel",
        "LgymApi.Application.WorkoutProgress.ProgressData.Models.MeasurementTrendReadModel",
        "LgymApi.Application.WorkoutProgress.ProgressData.Models.MainRecordReadModel",
        "LgymApi.Application.WorkoutProgress.ProgressData.Models.ProgressExerciseReadModel",
        "LgymApi.Application.WorkoutProgress.ProgressData.Models.MainRecordBestReadModel",
        "LgymApi.Application.WorkoutProgress.ProgressData.Models.PossibleRecordReadModel",
        "LgymApi.Application.WorkoutProgress.ProgressData.Models.EloChartPoint",
        "LgymApi.Application.WorkoutProgress.ProgressData.Models.MeasurementWriteModel",
        "LgymApi.Application.WorkoutProgress.ProgressData.Models.MainRecordCreateWriteModel",
        "LgymApi.Application.WorkoutProgress.ProgressData.Models.MainRecordUpdateWriteModel",
        "LgymApi.Application.WorkoutProgress.Dashboard.IWorkoutProgressDashboardReadService",
        "LgymApi.Application.WorkoutProgress.Dashboard.Models.WorkoutProgressDashboardTrainingReadModel",
        "LgymApi.Application.WorkoutProgress.Dashboard.Models.WorkoutProgressDashboardPlanDayReadModel",
        "LgymApi.Application.WorkoutProgress.Dashboard.Models.WorkoutProgressDashboardExerciseReadModel",
        "LgymApi.Application.WorkoutProgress.Dashboard.Models.WorkoutProgressDashboardExerciseDetailsReadModel",
        "LgymApi.Application.WorkoutProgress.Dashboard.Models.WorkoutProgressDashboardExerciseScoreReadModel",
        "LgymApi.Application.WorkoutProgress.Ranking.IWorkoutProgressRankingReadService",
        "LgymApi.Application.WorkoutProgress.Ranking.Models.RankingReadModel",
        "LgymApi.Application.WorkoutProgress.TrainingExecution.ICompleteTrainingUseCase",
        "LgymApi.Application.WorkoutProgress.TrainingExecution.CompleteTrainingInput",
        "LgymApi.Application.WorkoutProgress.TrainingExecution.ITrainingHistoryReadService",
        "LgymApi.Application.WorkoutProgress.Contracts.ReportingIntegration.IReportSubmissionAcceptedProgressConsumer",
        "LgymApi.Application.WorkoutProgress.Contracts.ReportingIntegration.ReportSubmissionAcceptedProgressEvent",
        "LgymApi.Application.WorkoutProgress.Contracts.ReportingIntegration.ReportSubmissionAcceptedMeasurement",
        "LgymApi.Application.WorkoutProgress.Contracts.ReportingIntegration.ReportSubmissionAcceptedProgressValidationResult",
        "LgymApi.Application.WorkoutProgress.Contracts.ReportingIntegration.ReportSubmissionAcceptedProgressConsumeResult"
    };

    [Test]
    public void Foreign_Modules_Should_Use_Only_Explicit_Workout_Progress_Contracts_And_Read_Models()
    {
        var (_, compilation, syntaxTrees) = ArchitectureTestHelpers.PrepareCompilation("LgymApi.Application");

        Assert.That(
            CollectViolations(compilation, syntaxTrees),
            Is.Empty,
            "Foreign modules must consume Workout & Progress only through its explicit contracts and read models.");
    }

    [Test]
    public void Direct_Workout_Progress_Entities_And_Repositories_Should_Be_Rejected()
    {
        var (repoRoot, compilation, _) = ArchitectureTestHelpers.PrepareCompilation("LgymApi.Application");
        var fixture = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("""
            using LgymApi.Application.Repositories;
            using LgymApi.Domain.Entities;

            namespace LgymApi.Application.TrainerRelationships;

            public sealed class ForeignWorkoutProgressPersistenceAccess
            {
                public IMainRecordRepository MainRecordRepository { get; init; } = default!;
                public IEloRegistryRepository EloRegistryRepository { get; init; } = default!;
                public IExerciseScoreRepository ExerciseScoreRepository { get; init; } = default!;
                public ITrainingRepository TrainingRepository { get; init; } = default!;
                public MainRecord MainRecord { get; init; } = default!;
                public EloRegistry EloRegistry { get; init; } = default!;
                public ExerciseScore ExerciseScore { get; init; } = default!;
                public Training Training { get; init; } = default!;
            }
            """, path: Path.Combine(repoRoot, "LgymApi.Application", "TrainerRelationships", "ForeignWorkoutProgressPersistenceAccess.cs"));

        var violations = CollectViolations(compilation.AddSyntaxTrees(fixture), [fixture]);

        Assert.That(
            violations.Select(violation => violation.TargetMetadataName),
            Is.EquivalentTo(new[]
            {
                "LgymApi.Application.Repositories.IMainRecordRepository",
                "LgymApi.Application.Repositories.IEloRegistryRepository",
                "LgymApi.Application.Repositories.IExerciseScoreRepository",
                "LgymApi.Application.Repositories.ITrainingRepository",
                "LgymApi.Domain.Entities.MainRecord",
                "LgymApi.Domain.Entities.EloRegistry",
                "LgymApi.Domain.Entities.ExerciseScore",
                "LgymApi.Domain.Entities.Training"
            }));
    }

    private static IReadOnlyList<Violation> CollectViolations(CSharpCompilation compilation, IEnumerable<SyntaxTree> syntaxTrees)
    {
        var violations = new Dictionary<string, Violation>(StringComparer.Ordinal);

        foreach (var tree in syntaxTrees)
        {
            var sourceModule = ArchitectureTestHelpers.GetCanonicalModuleNameFromPath(tree.FilePath);
            if (sourceModule is null || !ForeignModuleNames.Contains(sourceModule))
            {
                continue;
            }

            var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            foreach (var typeSyntax in tree.GetCompilationUnitRoot().DescendantNodes().OfType<TypeSyntax>())
            {
                var type = ArchitectureTestHelpers.GetOwnedNamedTypeSymbol(semanticModel.GetTypeInfo(typeSyntax).Type);
                if (type is null || !IsWorkoutProgressType(type))
                {
                    continue;
                }

                var targetMetadataName = type.OriginalDefinition
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    .Replace("global::", string.Empty, StringComparison.Ordinal);
                if (AllowedPublicContractMetadataNames.Contains(targetMetadataName))
                {
                    continue;
                }

                var violation = new Violation(sourceModule, targetMetadataName, tree.FilePath, typeSyntax.ToString());
                violations.TryAdd(violation.Identity, violation);
            }
        }

        return violations.Values.OrderBy(violation => violation.Identity, StringComparer.Ordinal).ToList();
    }

    private static bool IsWorkoutProgressType(INamedTypeSymbol type)
    {
        if (ArchitectureTestHelpers.GetCanonicalModuleNameForSymbol(type) == ArchitectureTestHelpers.WorkoutProgressModuleName)
        {
            return true;
        }

        return ArchitectureTestHelpers.TryGetPersistedEntityOwner(type, out var owner) &&
               owner == ArchitectureTestHelpers.WorkoutProgressModuleName;
    }

    private sealed record Violation(string SourceModule, string TargetMetadataName, string SourceFile, string SourceType)
    {
        public string Identity => $"{SourceModule}|{SourceFile}|{SourceType}|{TargetMetadataName}";

        public override string ToString() => $"{SourceModule}: {SourceType} references {TargetMetadataName} in {SourceFile}";
    }
}
