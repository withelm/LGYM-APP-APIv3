using System.Text.Json.Serialization;
using LgymApi.Application.Features.TrainerRelationships.Models;

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

public sealed class TrainerDashboardTraineesRequest
{
    [JsonPropertyName("search")]
    public string? Search { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("sortBy")]
    public string? SortBy { get; set; }

    [JsonPropertyName("sortDirection")]
    public string? SortDirection { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 20;
}

public sealed class TrainerDashboardTraineeDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }

    [JsonPropertyName("status")]
    public TrainerDashboardTraineeStatus Status { get; set; }

    [JsonPropertyName("isLinked")]
    public bool IsLinked { get; set; }

    [JsonPropertyName("hasPendingInvitation")]
    public bool HasPendingInvitation { get; set; }

    [JsonPropertyName("hasExpiredInvitation")]
    public bool HasExpiredInvitation { get; set; }

    [JsonPropertyName("linkedAt")]
    public DateTimeOffset? LinkedAt { get; set; }

    [JsonPropertyName("lastInvitationExpiresAt")]
    public DateTimeOffset? LastInvitationExpiresAt { get; set; }

    [JsonPropertyName("lastInvitationRespondedAt")]
    public DateTimeOffset? LastInvitationRespondedAt { get; set; }
}

public sealed class TrainerDashboardTraineesResponse
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("items")]
    public List<TrainerDashboardTraineeDto> Items { get; set; } = [];
}

public sealed class TrainerPlanFormRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class TrainerManagedPlanDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}
