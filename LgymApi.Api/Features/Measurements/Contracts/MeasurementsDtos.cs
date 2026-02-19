using System.Text.Json.Serialization;
using LgymApi.Api.Features.Enum.Contracts;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Features.Measurements.Contracts;

public sealed class MeasurementFormDto
{
    [JsonPropertyName("user")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("bodyPart")]
    [JsonRequired]
    public BodyParts BodyPart { get; set; }

    [JsonPropertyName("unit")]
    [JsonRequired]
    public HeightUnits Unit { get; set; }

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
    public BodyParts? BodyPart { get; set; }
}

public sealed class MeasurementsHistoryDto
{
    [JsonPropertyName("measurements")]
    public List<MeasurementResponseDto> Measurements { get; set; } = new();
}
