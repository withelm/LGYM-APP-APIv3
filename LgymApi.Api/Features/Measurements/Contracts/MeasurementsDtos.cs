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
    [JsonRequired]
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

    [JsonPropertyName("unit")]
    public HeightUnits? Unit { get; set; }
}

public sealed class MeasurementsHistoryDto
{
    [JsonPropertyName("measurements")]
    public List<MeasurementResponseDto> Measurements { get; set; } = new();
}

public sealed class MeasurementsListDto
{
    [JsonPropertyName("measurements")]
    public List<MeasurementResponseDto> Measurements { get; set; } = new();
}

public sealed class MeasurementTrendRequestDto
{
    [JsonPropertyName("bodyPart")]
    [JsonRequired]
    public BodyParts BodyPart { get; set; }

    [JsonPropertyName("unit")]
    [JsonRequired]
    public HeightUnits Unit { get; set; }
}

public sealed class MeasurementTrendDto
{
    [JsonPropertyName("bodyPart")]
    public EnumLookupDto BodyPart { get; set; } = new();

    [JsonPropertyName("unit")]
    public EnumLookupDto Unit { get; set; } = new();

    [JsonPropertyName("startValue")]
    public double StartValue { get; set; }

    [JsonPropertyName("currentValue")]
    public double CurrentValue { get; set; }

    [JsonPropertyName("change")]
    public double Change { get; set; }

    [JsonPropertyName("changePercentage")]
    public double ChangePercentage { get; set; }

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = string.Empty;

    [JsonPropertyName("points")]
    public int Points { get; set; }
}
