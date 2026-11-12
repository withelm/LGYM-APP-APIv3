using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Reporting.Contracts.BackgroundCommands;

public interface IRelationshipEndedReportCancellationActionExecutionPort
{
    Task ExecuteAsync(
        Id<CoachingEpisode> episodeId,
        CancellationToken cancellationToken = default);
}
