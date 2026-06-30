using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.Trainer.Contracts;

public sealed class UpsertTraineeNoteRequest : IDto
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("visibleToTrainee")]
    public bool VisibleToTrainee { get; set; }

    [JsonPropertyName("isPinned")]
    public bool IsPinned { get; set; }
}

public sealed class TraineeNoteDto : IResultDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("trainerId")]
    public string TrainerId { get; set; } = string.Empty;

    [JsonPropertyName("traineeId")]
    public string TraineeId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("visibleToTrainee")]
    public bool VisibleToTrainee { get; set; }

    [JsonPropertyName("isPinned")]
    public bool IsPinned { get; set; }

    [JsonPropertyName("lastUpdatedByUserId")]
    public string LastUpdatedByUserId { get; set; } = string.Empty;

    [JsonPropertyName("lastUpdatedAt")]
    public DateTimeOffset LastUpdatedAt { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class TraineeNoteHistoryDto : IResultDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("traineeNoteId")]
    public string TraineeNoteId { get; set; } = string.Empty;

    [JsonPropertyName("changedByUserId")]
    public string ChangedByUserId { get; set; } = string.Empty;

    [JsonPropertyName("changedAt")]
    public DateTimeOffset ChangedAt { get; set; }

    [JsonPropertyName("previousContent")]
    public string? PreviousContent { get; set; }

    [JsonPropertyName("newContent")]
    public string NewContent { get; set; } = string.Empty;

    [JsonPropertyName("changeType")]
    public string ChangeType { get; set; } = string.Empty;
}
