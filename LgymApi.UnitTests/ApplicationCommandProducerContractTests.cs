using System.IO;
using FluentAssertions;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ApplicationCommandProducerContractTests
{
    public static IEnumerable<TestCaseData> ProducerContractCases()
    {
        yield return CreateCase("WorkoutProgress/TrainingExecution/CompleteTrainingUseCase.cs", "LgymApi.Application.WorkoutProgress.Contracts.BackgroundCommands");
        yield return CreateCase("Features/DietPlans/DietPlanService.cs", "LgymApi.Application.Platform.Contracts.BackgroundCommands", "LgymApi.Application.Nutrition.Contracts.BackgroundCommands");
        yield return CreateCase("Features/TraineeNotes/TraineeNoteService.cs", "LgymApi.Application.Platform.Contracts.BackgroundCommands", "LgymApi.Application.Coaching.Contracts.BackgroundCommands");
        yield return CreateCase("Features/Reporting/ReportingService.cs", "LgymApi.Application.Platform.Contracts.BackgroundCommands");
        yield return CreateCase("Features/Reporting/ReportingService.Requests.cs", "LgymApi.Application.Reporting.Contracts.BackgroundCommands");
        yield return CreateCase("Features/Reporting/ReportingService.Submissions.cs", "LgymApi.Application.Reporting.Contracts.BackgroundCommands");
        yield return CreateCase("Features/Reporting/RecurringReportAssignmentService.cs", "LgymApi.Application.Platform.Contracts.BackgroundCommands", "LgymApi.Application.Reporting.Contracts.BackgroundCommands");
        yield return CreateCase("Features/Reporting/IReportingServiceDependencies.cs", "LgymApi.Application.Platform.Contracts.BackgroundCommands");
        yield return CreateCase("Features/Reporting/IRecurringReportAssignmentServiceDependencies.cs", "LgymApi.Application.Platform.Contracts.BackgroundCommands");
        yield return CreateCase("TrainerRelationships/TrainerRelationshipService.cs", "LgymApi.Application.Platform.Contracts.BackgroundCommands");
        yield return CreateCase("TrainerRelationships/TrainerRelationshipService.InvitationCreation.cs", "LgymApi.Application.Coaching.Contracts.BackgroundCommands");
        yield return CreateCase("TrainerRelationships/TrainerRelationshipService.InvitationLifecycle.cs", "LgymApi.Application.Coaching.Contracts.BackgroundCommands");
        yield return CreateCase("TrainerRelationships/TrainerRelationshipService.Links.cs", "LgymApi.Application.Coaching.Contracts.BackgroundCommands");
        yield return CreateCase("TrainerRelationships/ITrainerRelationshipServiceDependencies.cs", "LgymApi.Application.Platform.Contracts.BackgroundCommands");
    }

    [TestCaseSource(nameof(ProducerContractCases))]
    public void Producer_UsesOnlyPlatformAndModuleOwnedCommandContracts(ProducerContractExpectation expectation)
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "LgymApi.Application", expectation.RelativePath));

        source.Should().NotContain("LgymApi.BackgroundWorker.Common");
        foreach (var requiredNamespace in expectation.RequiredNamespaces)
        {
            source.Should().Contain($"using {requiredNamespace};");
        }
    }

    private static TestCaseData CreateCase(string relativePath, params string[] requiredNamespaces)
        => new TestCaseData(new ProducerContractExpectation(relativePath, requiredNamespaces))
            .SetName(relativePath.Replace('/', '_'));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LgymApi.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test directory.");
    }

    public sealed record ProducerContractExpectation(string RelativePath, IReadOnlyCollection<string> RequiredNamespaces);
}
