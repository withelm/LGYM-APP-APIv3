using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.Coaching.Contracts.BackgroundCommands;

public sealed class RelationshipEndedReportCancellationCommand : IActionCommand
{
    public Id<CoachingEpisode> EpisodeId { get; init; }
}
