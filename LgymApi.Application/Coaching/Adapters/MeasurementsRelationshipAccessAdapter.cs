using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.WorkoutProgress.Contracts.Measurements;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Coaching.Adapters;

internal sealed class MeasurementsRelationshipAccessAdapter(
    ICoachingRelationshipAccessService relationshipAccessService) : IMeasurementsRelationshipAccessPort
{
    public async Task<bool> HasActiveRelationshipAsync(
        Id<UserEntity> trainerId,
        Id<UserEntity> traineeId,
        CancellationToken cancellationToken = default)
    {
        var decision = await relationshipAccessService.GetAccessDecisionAsync(
            trainerId,
            traineeId,
            cancellationToken);

        return decision.HasActiveRelationship;
    }
}
