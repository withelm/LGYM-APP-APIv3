using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class TraineeNoteHistory : EntityBase<TraineeNoteHistory>
{
    public Id<TraineeNote> TraineeNoteId { get; set; }
    public Id<User> ChangedByUserId { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string? PreviousContent { get; set; }
    public string NewContent { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;

    public TraineeNote TraineeNote { get; set; } = null!;
    public User ChangedByUser { get; set; } = null!;
}
