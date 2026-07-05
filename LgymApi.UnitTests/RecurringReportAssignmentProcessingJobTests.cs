using FluentAssertions;
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
}
