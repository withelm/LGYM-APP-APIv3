using LgymApi.Application.Coaching.Persistence;
using LgymApi.Application.WorkoutProgress.Contracts.Measurements;
using LgymApi.Domain.ValueObjects;
using LgymApi.Domain.Security;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;

namespace LgymApi.Application.Coaching.Adapters;

internal sealed class MeasurementsRelationshipAccessAdapter(
    IAccountAccessReader accountAccess,
    ICoachingActiveLinkPersistence activeLinks) : IMeasurementsRelationshipAccessPort
{
    public async Task<MeasurementsRelationshipAccessFact> GetAccessAsync(
        Id<AccountReference> trainerId,
        Id<AccountReference> traineeId,
        CancellationToken cancellationToken = default)
    {
        var trainer = await accountAccess.GetByIdAsync(trainerId, cancellationToken);
        if (trainer?.Roles.Contains(AuthConstants.Roles.Trainer, StringComparer.Ordinal) != true)
        {
            return MeasurementsRelationshipAccessFact.None;
        }

        var access = await activeLinks.GetMeasurementsAccessAsync(trainerId, traineeId, cancellationToken);
        var episodes = access.HasActiveRelationship
            ? access.Episodes
            : access.Episodes.Where(episode => episode.EndedAt is not null);
        return new MeasurementsRelationshipAccessFact(
            access.HasActiveRelationship,
            episodes.Select(episode => new MeasurementsRelationshipPeriod(
                episode.Id,
                episode.StartedAt,
                episode.EndedAt)).ToArray());
    }
}
