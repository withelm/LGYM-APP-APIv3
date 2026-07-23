using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.Coaching.Persistence;
using LgymApi.Application.Identity.Contracts.Access;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Coaching.Access;

internal sealed class CoachingRelationshipAccessService : ICoachingRelationshipAccessService
{
    private readonly IUserAccessReadService _userAccess;
    private readonly ICoachingActiveLinkPersistence _activeLinks;

    public CoachingRelationshipAccessService(
        IUserAccessReadService userAccess,
        ICoachingActiveLinkPersistence activeLinks)
    {
        _userAccess = userAccess;
        _activeLinks = activeLinks;
    }

    public async Task<CoachingRelationshipAccessDecision> GetAccessDecisionAsync(
        Id<UserEntity> trainerId,
        Id<UserEntity> traineeId,
        CancellationToken cancellationToken = default)
    {
        if (trainerId.IsEmpty)
        {
            return new CoachingRelationshipAccessDecision(false, false);
        }

        var isTrainer = await _userAccess.IsTrainerAsync(trainerId, cancellationToken);
        if (!isTrainer || traineeId.IsEmpty)
        {
            return new CoachingRelationshipAccessDecision(isTrainer, false);
        }

        var activeLink = await _activeLinks.FindByTrainerAndTraineeAsync(trainerId, traineeId, cancellationToken);
        return new CoachingRelationshipAccessDecision(true, activeLink is not null);
    }
}
