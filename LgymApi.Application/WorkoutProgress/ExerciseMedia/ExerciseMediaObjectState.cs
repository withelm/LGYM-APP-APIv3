using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.Application.WorkoutProgress.ExerciseMedia;

public sealed class ExerciseMediaObjectState
{
    public Id<ExerciseMediaObjectFence> Id { get; set; }
    public Id<Exercise> ExerciseId { get; set; }
    public string ObjectKey { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public long Revision { get; set; }
    public bool IsRetired { get; set; }
    public DateTimeOffset? LastCleanupAt { get; set; }
    public bool LastDeleteSucceeded { get; set; }
}
