using System.Text.Json.Serialization;

namespace LgymApi.Api.Features.EloRegistry.Contracts;

public sealed class EloRegistryBaseChartDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public int Value { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;
}
