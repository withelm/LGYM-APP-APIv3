using LgymApi.Application.Coaching.Contracts.Access;
using LgymApi.Application.Coaching.Persistence;
using LgymApi.Authorization.Contracts;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Platform.Contracts;

namespace LgymApi.Application.Coaching.Access;

internal sealed class CoachingEpisodeAuthorizationService(
    ICoachingActiveLinkPersistence activeLinks,
    IAuthorizationEvaluator<CoachingEpisodeAuthorizationContext> evaluator)
    : ICoachingEpisodeAuthorizationService
{
    public async Task<CoachingEpisodeAuthorizationGrant?> AuthorizeAndStageAsync(
        Id<User> actorId,
        Id<User> trainerId,
        Id<User> traineeId,
        CoachingEpisodeAuthorizationAction action,
        CancellationToken cancellationToken = default)
    {
        var link = await activeLinks.FindByTrainerAndTraineeAsync(trainerId, traineeId, cancellationToken);
        return await AuthorizeAndStageAsync(actorId, link, action, cancellationToken);
    }

    public Task<CoachingEpisodeAuthorizationGrant?> AuthorizeAndStageAsync(
        Id<AccountReference> actorId,
        Id<AccountReference> trainerId,
        Id<AccountReference> traineeId,
        CoachingEpisodeAuthorizationAction action,
        CancellationToken cancellationToken = default)
        => AuthorizeAndStageAsync(
            actorId.Rebind<User>(),
            trainerId.Rebind<User>(),
            traineeId.Rebind<User>(),
            action,
            cancellationToken);

    public async Task<CoachingEpisodeAuthorizationGrant?> AuthorizeTraineeDetachAndStageAsync(
        Id<User> traineeId,
        CancellationToken cancellationToken = default)
    {
        var link = await activeLinks.FindByTraineeAsync(traineeId, cancellationToken);
        return await AuthorizeAndStageAsync(
            traineeId,
            link,
            CoachingEpisodeAuthorizationAction.TraineeDetach,
            cancellationToken);
    }

    private async Task<CoachingEpisodeAuthorizationGrant?> AuthorizeAndStageAsync(
        Id<User> actorId,
        CoachingActiveLinkFact? link,
        CoachingEpisodeAuthorizationAction action,
        CancellationToken cancellationToken)
    {
        if (link?.EpisodeId is not { } episodeId || link.AuthorizationRevision is not { } revision)
        {
            return null;
        }

        var context = new CoachingEpisodeAuthorizationContext(
            link.TrainerId.Rebind<ActorReference>(),
            link.TraineeId.Rebind<ActorReference>(),
            episodeId,
            revision,
            action);
        var actor = new AuthorizationActor(actorId.Rebind<ActorReference>());
        if (!evaluator.Evaluate(actor, context).IsAllowed)
        {
            return null;
        }

        var grant = new CoachingEpisodeAuthorizationGrant(
            link.Id,
            episodeId,
            link.TrainerId,
            link.TraineeId,
            revision);
        return await activeLinks.StageRevisionCheckAsync(grant, cancellationToken)
            ? grant
            : null;
    }
}

internal sealed record CoachingEpisodeAuthorizationContext(
    Id<ActorReference> TrainerId,
    Id<ActorReference> TraineeId,
    Id<CoachingEpisode> EpisodeId,
    long AuthorizationRevision,
    CoachingEpisodeAuthorizationAction Action);

internal sealed class CoachingEpisodeAuthorizationPolicy
    : IAuthorizationPolicy<CoachingEpisodeAuthorizationContext>
{
    public AuthorizationDecision Evaluate(
        AuthorizationActor actor,
        CoachingEpisodeAuthorizationContext context)
    {
        var expectedActor = context.Action == CoachingEpisodeAuthorizationAction.TraineeDetach
            ? context.TraineeId
            : context.TrainerId;
        return actor.Id == expectedActor
            && !context.EpisodeId.IsEmpty
            && context.AuthorizationRevision >= 0
                ? AuthorizationDecision.Allow
                : AuthorizationDecision.Deny;
    }
}
