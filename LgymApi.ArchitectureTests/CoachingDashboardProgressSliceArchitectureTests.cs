using System.Reflection;
using LgymApi.Application.Coaching;
using LgymApi.Application.Coaching.Progress.EloChart;
using LgymApi.Application.Coaching.Progress.ExerciseScoresChart;
using LgymApi.Application.Coaching.Progress.MainRecordsHistory;
using LgymApi.Application.Coaching.Progress.TrainingByDate;
using LgymApi.Application.Coaching.Progress.TrainingDates;
using LgymApi.Application.Coaching.Relationships.TrainerDashboard;
using LgymApi.Application.WorkoutProgress.Dashboard;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class CoachingDashboardProgressSliceArchitectureTests
{
    private static readonly (Type Contract, string ImplementationName)[] Slices =
    [
        (typeof(IGetTrainerDashboardUseCase), "LgymApi.Application.Coaching.Relationships.TrainerDashboard.GetTrainerDashboardUseCase"),
        (typeof(IGetTrainingDatesUseCase), "LgymApi.Application.Coaching.Progress.TrainingDates.GetTrainingDatesUseCase"),
        (typeof(IGetTrainingByDateUseCase), "LgymApi.Application.Coaching.Progress.TrainingByDate.GetTrainingByDateUseCase"),
        (typeof(IGetExerciseScoresChartUseCase), "LgymApi.Application.Coaching.Progress.ExerciseScoresChart.GetExerciseScoresChartUseCase"),
        (typeof(IGetEloChartUseCase), "LgymApi.Application.Coaching.Progress.EloChart.GetEloChartUseCase"),
        (typeof(IGetMainRecordsHistoryUseCase), "LgymApi.Application.Coaching.Progress.MainRecordsHistory.GetMainRecordsHistoryUseCase")
    ];

    [Test]
    public void DashboardAndProgressSlices_ExposeOneMethodContractsWithInternalImplementations()
    {
        var assembly = typeof(IGetTrainerDashboardUseCase).Assembly;

        foreach (var slice in Slices)
        {
            Assert.That(slice.Contract.IsPublic, Is.True);
            Assert.That(slice.Contract.GetMethods(BindingFlags.Public | BindingFlags.Instance), Has.Exactly(1).Items);
            Assert.That(assembly.GetType(slice.ImplementationName)!.IsNotPublic, Is.True);
        }
    }

    [Test]
    public void DashboardAndProgressSlices_AreRegisteredExactlyOnceByCoachingModule()
    {
        var services = new ServiceCollection();
        services.AddCoachingModule();

        foreach (var slice in Slices)
        {
            Assert.That(services.Count(descriptor => descriptor.ServiceType == slice.Contract), Is.EqualTo(1));
        }
    }

    [Test]
    public void DashboardAndProgressSlices_DoNotUseForeignRepositoriesOrEntityValues()
    {
        var root = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var coachingRoot = Path.Combine(root, "LgymApi.Application", "Coaching");
        var files = Directory.GetFiles(Path.Combine(coachingRoot, "Progress"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(coachingRoot, "Relationships", "TrainerDashboard"), "*.cs", SearchOption.AllDirectories));
        var source = files.Select(File.ReadAllText).ToArray();

        Assert.That((source).Any(text => text.Contains("IUserRepository", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains("ITrainerRelationshipRepository", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains("ITrainingRepository", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains("IExerciseScoreRepository", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains("IMainRecordRepository", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains(" User ", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void TrainerDashboard_DelegatesFactAndAccountTransformationToRegisteredMapper()
    {
        var root = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var directory = Path.Combine(root, "LgymApi.Application", "Coaching", "Relationships", "TrainerDashboard");
        var useCase = File.ReadAllText(Path.Combine(directory, "GetTrainerDashboardUseCase.cs"));
        var profile = File.ReadAllText(Path.Combine(directory, "TrainerDashboardMappingProfile.cs"));

        Assert.That(useCase, Does.Not.Contain("new TrainerDashboardTraineeReadModel("));
        Assert.That(useCase, Does.Contain("_mapper.MapList<TrainerDashboardSource, TrainerDashboardTraineeReadModel>"));
        Assert.That(useCase, Does.Contain("_mapper.CreateContext()"));
        Assert.That(profile, Does.Contain("CreateMap<TrainerDashboardSource, TrainerDashboardTraineeReadModel>"));
    }

    [Test]
    public void ExerciseScoresChart_KeepsTypedExerciseIdUntilWorkoutProgressFacadeCall()
    {
        Assert.That(typeof(GetExerciseScoresChartQuery).GetProperty(nameof(GetExerciseScoresChartQuery.ExerciseId))!
            .PropertyType, Is.EqualTo(typeof(Id<Exercise>)));
        Assert.That(typeof(IWorkoutProgressDashboardReadService)
            .GetMethod(nameof(IWorkoutProgressDashboardReadService.GetExerciseScoreChartAsync))!
            .GetParameters()[1].ParameterType, Is.EqualTo(typeof(string)));

        var root = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var useCaseRoot = ParseRoot(Path.Combine(
            root,
            "LgymApi.Application",
            "Coaching",
            "Progress",
            "ExerciseScoresChart",
            "GetExerciseScoresChartUseCase.cs"));
        var facadeCall = useCaseRoot.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Single(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Name.Identifier.ValueText == nameof(IWorkoutProgressDashboardReadService.GetExerciseScoreChartAsync));
        Assert.That(facadeCall.ArgumentList.Arguments[1].Expression.ToString(), Is.EqualTo("query.ExerciseId.ToString()"));
        var assertedValue1 = (FindToStringCalls(useCaseRoot)).ToArray();
        Assert.That(assertedValue1, Has.Exactly(1).Items);
        Assert.That(assertedValue1.Single(), Is.SameAs(facadeCall.ArgumentList.Arguments[1].Expression));

        var controllerRoot = ParseRoot(Path.Combine(
            root,
            "LgymApi.Api",
            "Features",
            "Trainer",
            "Controllers",
            "TrainerDashboardProgressController.cs"));
        var controllerAction = controllerRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method => method.Identifier.ValueText == "GetTraineeExerciseScoresChartData");
        Assert.That(FindToStringCalls(controllerAction), Is.Empty);
    }

    private static CompilationUnitSyntax ParseRoot(string path) =>
        CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetCompilationUnitRoot();

    private static IEnumerable<InvocationExpressionSyntax> FindToStringCalls(SyntaxNode root) =>
        root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Name.Identifier.ValueText == nameof(ToString));
}
