using System.Text.Json.Serialization;

namespace LgymApi.Api.Features.Trainer.Contracts;

public sealed class CreateTrainerInvitationRequest
{
    [JsonPropertyName("traineeId")]
    public string TraineeId { get; set; } = string.Empty;
}

public sealed class TrainerInvitationDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("trainerId")]
    public string TrainerId { get; set; } = string.Empty;

    [JsonPropertyName("traineeId")]
    public string TraineeId { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset ExpiresAt { get; set; }

    [JsonPropertyName("respondedAt")]
    public DateTimeOffset? RespondedAt { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}
