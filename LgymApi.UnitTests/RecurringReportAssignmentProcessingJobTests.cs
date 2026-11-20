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

        Assert.That(attributeData, Is.Not.Null, "ExecuteAsync must prevent overlapping recurring job runs");
        Assert.That(attributeData!.ConstructorArguments, Has.Exactly(1).Items);
        Assert.That(attributeData.ConstructorArguments[0].Value, Is.EqualTo(300));
    }
}
