using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class TraineeNote : EntityBase<TraineeNote>
{
    public Id<User> TrainerId { get; set; }
    public Id<User> TraineeId { get; set; }
    public string? Title { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool VisibleToTrainee { get; set; }
    public bool IsPinned { get; set; }
    public Id<User> LastUpdatedByUserId { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }

    public User Trainer { get; set; } = null!;
    public User Trainee { get; set; } = null!;
    public User LastUpdatedByUser { get; set; } = null!;
    public ICollection<TraineeNoteHistory> HistoryEntries { get; set; } = new List<TraineeNoteHistory>();
}
