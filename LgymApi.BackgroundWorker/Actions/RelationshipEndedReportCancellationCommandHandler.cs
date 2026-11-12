using LgymApi.Application.Coaching.Contracts.BackgroundCommands;
using LgymApi.Application.Reporting.Contracts.BackgroundCommands;
using LgymApi.BackgroundWorker.Actions.Contracts;

namespace LgymApi.BackgroundWorker.Actions;

public sealed class RelationshipEndedReportCancellationCommandHandler(
    IRelationshipEndedReportCancellationActionExecutionPort port)
    : IBackgroundAction<RelationshipEndedReportCancellationCommand>
{
    public Task ExecuteAsync(
        RelationshipEndedReportCancellationCommand command,
        CancellationToken cancellationToken = default) =>
        port.ExecuteAsync(command.EpisodeId, cancellationToken);
}
