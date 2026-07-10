using FluentAssertions;
using Hangfire;
using LgymApi.Application.Features.Reporting;
using LgymApi.BackgroundWorker.Jobs;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class RecurringReportAssignmentProcessingJobTests
{
    [Test]
    public async Task ExecuteAsync_ForwardsToService()
    {
        var service = Substitute.For<IRecurringReportAssignmentService>();
        var job = new RecurringReportAssignmentProcessingJob(service);
        using var cancellation = new CancellationTokenSource();

        await job.ExecuteAsync(cancellation.Token);

        await service.Received(1).ProcessDueAssignmentsAsync(cancellation.Token);
    }

    [Test]
    public void ExecuteAsync_HasDisableConcurrentExecutionAttributeWith300SecondTimeout()
    {
        var method = typeof(RecurringReportAssignmentProcessingJob).GetMethod(nameof(RecurringReportAssignmentProcessingJob.ExecuteAsync));
        var attributeData = method?.CustomAttributes.FirstOrDefault(attribute => attribute.AttributeType == typeof(DisableConcurrentExecutionAttribute));

        attributeData.Should().NotBeNull("ExecuteAsync must prevent overlapping recurring job runs");
        attributeData!.ConstructorArguments.Should().ContainSingle();
        attributeData.ConstructorArguments[0].Value.Should().Be(300);
    }
}
