using System.Text.Json.Serialization;

namespace LgymApi.Api.DTOs;

public sealed class EloRegistryBaseChartDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public int Value { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;
}
