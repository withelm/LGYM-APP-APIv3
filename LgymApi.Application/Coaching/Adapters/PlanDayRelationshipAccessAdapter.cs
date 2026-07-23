using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.TrainingPlanning.Contracts.PlanDay;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Coaching.Adapters;

internal sealed class PlanDayRelationshipAccessAdapter(
    ICoachingRelationshipAccessService relationshipAccessService) : IPlanDayRelationshipAccessPort
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
