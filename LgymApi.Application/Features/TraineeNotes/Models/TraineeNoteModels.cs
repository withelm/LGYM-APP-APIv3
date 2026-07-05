using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.TraineeNotes.Models;

public sealed class UpsertTraineeNoteCommand
{
    public string? Title { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool VisibleToTrainee { get; set; }
    public bool IsPinned { get; set; }
}

public sealed class TraineeNoteResult
{
    public Id<TraineeNote> Id { get; set; }
    public Id<UserEntity> TrainerId { get; set; }
    public Id<UserEntity> TraineeId { get; set; }
    public string? Title { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool VisibleToTrainee { get; set; }
    public bool IsPinned { get; set; }
    public Id<UserEntity> LastUpdatedByUserId { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class TraineeNoteHistoryResult
{
    public Id<TraineeNoteHistory> Id { get; set; }
    public Id<TraineeNote> TraineeNoteId { get; set; }
    public Id<UserEntity> ChangedByUserId { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string? PreviousContent { get; set; }
    public string NewContent { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
}
