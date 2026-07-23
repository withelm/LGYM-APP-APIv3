using LgymApi.Application.Reporting.Contracts.BackgroundCommands;
using LgymApi.Application.WorkoutProgress.Contracts.ReportingIntegration;
using LgymApi.BackgroundWorker.Actions.Contracts;

namespace LgymApi.BackgroundWorker.Actions;

public sealed class ReportSubmissionAcceptedProgressCommandHandler :
    IBackgroundAction<ReportSubmissionAcceptedProgressCommand>
{
    private readonly IReportSubmissionAcceptedProgressConsumer _consumer;

    public ReportSubmissionAcceptedProgressCommandHandler(
        IReportSubmissionAcceptedProgressConsumer consumer)
    {
        _consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
    }

    public async Task ExecuteAsync(
        ReportSubmissionAcceptedProgressCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = await _consumer.ConsumeAsync(command.Event, cancellationToken);
        if (result.Outcome is ReportSubmissionAcceptedProgressConsumeOutcome.Applied
            or ReportSubmissionAcceptedProgressConsumeOutcome.Duplicate)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Report submission accepted-progress command delivery failed with outcome {result.Outcome}.");
    }
}
