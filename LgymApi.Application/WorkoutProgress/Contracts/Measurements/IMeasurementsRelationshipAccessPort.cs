using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.Application.WorkoutProgress.Contracts.Measurements;

public interface IMeasurementsRelationshipAccessPort
{
    Task<MeasurementsRelationshipAccessFact> GetAccessAsync(
        Id<AccountReference> trainerId,
        Id<AccountReference> traineeId,
        CancellationToken cancellationToken = default);
}

public sealed record MeasurementsRelationshipPeriod(
    Id<LgymApi.Domain.Entities.CoachingEpisode> EpisodeId,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt);

public sealed record MeasurementsRelationshipAccessFact(
    bool HasActiveRelationship,
    IReadOnlyList<MeasurementsRelationshipPeriod> Periods)
{
    public static readonly MeasurementsRelationshipAccessFact None = new(false, []);
}
