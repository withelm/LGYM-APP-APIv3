using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

/// <summary>A permanent fence for one immutable exercise object key; never a reusable upload slot.</summary>
public sealed class ExerciseMediaObjectFence : EntityBase<ExerciseMediaObjectFence>
{
    public Id<Exercise> ExerciseId { get; set; }
    public string ObjectKey { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public long Revision { get; set; }
    public bool IsRetired { get; set; }
    public DateTimeOffset? LastCleanupAt { get; set; }
    public bool LastDeleteSucceeded { get; set; }
}
