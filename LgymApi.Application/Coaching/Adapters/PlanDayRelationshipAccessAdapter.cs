using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.TrainingPlanning.Contracts.PlanDay;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.Application.Coaching.Adapters;

internal sealed class PlanDayRelationshipAccessAdapter(
    IMarkerCoachingRelationshipAccessService relationshipAccessService,
    ICoachingEpisodeAuthorizationService episodeAuthorization) : IPlanDayRelationshipAccessPort
{
    public async Task<bool> HasActiveRelationshipAsync(
        Id<AccountReference> trainerId,
        Id<AccountReference> traineeId,
        CancellationToken cancellationToken = default)
    {
        var decision = await relationshipAccessService.GetAccessDecisionAsync(
            trainerId,
            traineeId,
            cancellationToken);

        return decision.HasActiveRelationship;
    }

    public async Task<bool> AuthorizeWriteAsync(
        Id<AccountReference> trainerId,
        Id<AccountReference> traineeId,
        CancellationToken cancellationToken = default)
        => await episodeAuthorization.AuthorizeAndStageAsync(
            trainerId, trainerId, traineeId, CoachingEpisodeAuthorizationAction.ProtectedWrite, cancellationToken) is not null;
}
