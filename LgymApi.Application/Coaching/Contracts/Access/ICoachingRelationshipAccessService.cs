using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Coaching.Contracts.Access;

public sealed record CoachingRelationshipAccessDecision(
    bool IsTrainer,
    bool HasActiveRelationship);

public interface ICoachingRelationshipAccessService
{
    Task<CoachingRelationshipAccessDecision> GetAccessDecisionAsync(
        Id<UserEntity> trainerId,
        Id<UserEntity> traineeId,
        CancellationToken cancellationToken = default);
}
