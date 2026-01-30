using System.Text.Json.Serialization;

namespace LgymApi.Api.DTOs;

public sealed class MeasurementFormDto
{
    [JsonPropertyName("user")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("bodyPart")]
    public string BodyPart { get; set; } = string.Empty;

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}

public sealed class MeasurementResponseDto
{
    [JsonPropertyName("user")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("bodyPart")]
    public EnumLookupDto BodyPart { get; set; } = new();

    [JsonPropertyName("unit")]
    public EnumLookupDto Unit { get; set; } = new();

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}

public sealed class MeasurementsHistoryRequestDto
{
    [JsonPropertyName("bodyPart")]
    public string BodyPart { get; set; } = string.Empty;
}

public sealed class MeasurementsHistoryDto
{
    [JsonPropertyName("measurements")]
    public List<MeasurementResponseDto> Measurements { get; set; } = new();
}
