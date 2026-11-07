using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;

namespace LgymApi.Application.Coaching.Contracts.Access;

public enum CoachingEpisodeAuthorizationAction
{
    ProtectedWrite,
    TrainerUnlink,
    TraineeDetach
}

public sealed record CoachingEpisodeAuthorizationGrant(
    Id<TrainerTraineeLink> LinkId,
    Id<CoachingEpisode> EpisodeId,
    Id<User> TrainerId,
    Id<User> TraineeId,
    long AuthorizationRevision);

public interface ICoachingEpisodeAuthorizationService
{
    Task<CoachingEpisodeAuthorizationGrant?> AuthorizeAndStageAsync(
        Id<User> actorId,
        Id<User> trainerId,
        Id<User> traineeId,
        CoachingEpisodeAuthorizationAction action,
        CancellationToken cancellationToken = default);

    Task<CoachingEpisodeAuthorizationGrant?> AuthorizeAndStageAsync(
        Id<AccountReference> actorId,
        Id<AccountReference> trainerId,
        Id<AccountReference> traineeId,
        CoachingEpisodeAuthorizationAction action,
        CancellationToken cancellationToken = default);

    Task<CoachingEpisodeAuthorizationGrant?> AuthorizeTraineeDetachAndStageAsync(
        Id<User> traineeId,
        CancellationToken cancellationToken = default);
}
