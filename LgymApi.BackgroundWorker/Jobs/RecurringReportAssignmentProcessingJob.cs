using LgymApi.Application.Features.Reporting;
using LgymApi.BackgroundWorker.Common.Jobs;

namespace LgymApi.BackgroundWorker.Jobs;

public sealed class RecurringReportAssignmentProcessingJob : IRecurringReportAssignmentProcessingJob
{
    private readonly IRecurringReportAssignmentService _recurringReportAssignmentService;

    public RecurringReportAssignmentProcessingJob(IRecurringReportAssignmentService recurringReportAssignmentService)
    {
        _recurringReportAssignmentService = recurringReportAssignmentService;
    }

    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return _recurringReportAssignmentService.ProcessDueAssignmentsAsync(cancellationToken);
    }
}
