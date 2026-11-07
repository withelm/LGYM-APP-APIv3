using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class CoachingEpisode : EntityBase<CoachingEpisode>
{
    public Id<User> TrainerId { get; set; }
    public Id<User> TraineeId { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public CoachingEpisodeStartProvenance StartedAtProvenance { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public long AuthorizationRevision { get; set; }

    public User Trainer { get; set; } = null!;
    public User Trainee { get; set; } = null!;
}
