using FluentAssertions;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class ReportingProgressIntegrationCutoverGuardTests
{
    [Test]
    public void ReportingService_HasNoProductionDependencyOnTheFutureProgressConsumer()
    {
        var reportingDirectory = Path.Combine(
            ArchitectureTestHelpers.ResolveRepositoryRoot(),
            "LgymApi.Application",
            "Features",
            "Reporting");
        var reportingServiceSources = Directory
            .EnumerateFiles(reportingDirectory, "ReportingService*.cs", SearchOption.TopDirectoryOnly)
            .Select(File.ReadAllText)
            .ToArray();

        reportingServiceSources.Should().NotBeEmpty();
        reportingServiceSources.Should().NotContain(source =>
            source.Contains("IReportSubmissionAcceptedProgressConsumer", StringComparison.Ordinal)
            || source.Contains("WorkoutProgress.Contracts.ReportingIntegration", StringComparison.Ordinal)
            || source.Contains("ReportSubmissionAcceptedProgressEvent", StringComparison.Ordinal));

        var serviceCollectionSources = ArchitectureTestHelpers
            .EnumerateProjectSourceFiles("LgymApi.Application")
            .Where(path => Path.GetFileName(path).EndsWith("ServiceCollectionExtensions.cs", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToArray();

        serviceCollectionSources.Should().NotContain(source =>
            source.Contains("IReportSubmissionAcceptedProgressConsumer", StringComparison.Ordinal));
    }
}
