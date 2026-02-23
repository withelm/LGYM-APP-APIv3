using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.Gym.Contracts;

public sealed class GymFormDto : IDto, IResultDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("_id")]
    public string? Id { get; set; }
}

public sealed class LastTrainingGymPlanDayInfoDto : IResultDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class LastTrainingGymInfoDto : IResultDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("type")]
    public LastTrainingGymPlanDayInfoDto? Type { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class GymChoiceInfoDto : IResultDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("lastTrainingInfo")]
    public LastTrainingGymInfoDto? LastTrainingInfo { get; set; }
}
