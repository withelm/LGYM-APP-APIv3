using System.Text.Json.Serialization;

namespace LgymApi.Api.DTOs;

public sealed class PlanFormDto
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; set; }
}
