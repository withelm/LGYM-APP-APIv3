using System.Text.Json.Serialization;

namespace LgymApi.Api.DTOs;

public class GymFormDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("_id")]
    public string? Id { get; set; }
}

public sealed class LastTrainingGymPlanDayInfoDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class LastTrainingGymInfoDto
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

public class GymChoiceInfoDto : GymFormDto
{
    [JsonPropertyName("lastTrainingInfo")]
    public LastTrainingGymInfoDto? LastTrainingInfo { get; set; }
}
