using System.Text.Json.Serialization;
using LgymApi.Api.Features.Enum.Contracts;
using LgymApi.Api.Interfaces;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Features.Measurements.Contracts;

public sealed class MeasurementFormDto : IDto
{
    [JsonPropertyName("user")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("bodyPart")]
    [JsonRequired]
    public BodyParts BodyPart { get; set; }

    [JsonPropertyName("unit")]
    [JsonRequired]
    public MeasurementUnits Unit { get; set; }

    [JsonPropertyName("value")]
    [JsonRequired]
    public double Value { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}

public sealed class MeasurementResponseDto : IResultDto
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

public sealed class MeasurementsBulkFormDto : IDto
{
    [JsonPropertyName("measurements")]
    [JsonRequired]
    public List<MeasurementFormDto> Measurements { get; set; } = new();
}

public sealed class MeasurementsHistoryRequestDto : IDto
{
    [JsonPropertyName("bodyPart")]
    public BodyParts? BodyPart { get; set; }

    [JsonPropertyName("unit")]
    public MeasurementUnits? Unit { get; set; }
}

public sealed class MeasurementsHistoryDto : IResultDto
{
    [JsonPropertyName("measurements")]
    public List<MeasurementResponseDto> Measurements { get; set; } = new();
}

public sealed class MeasurementsListDto : IResultDto
{
    [JsonPropertyName("measurements")]
    public List<MeasurementResponseDto> Measurements { get; set; } = new();
}

public sealed class MeasurementTrendsDto : IResultDto
{
    [JsonPropertyName("trends")]
    public List<MeasurementTrendDto> Trends { get; set; } = new();
}

public sealed class MeasurementTrendRequestDto : IDto
{
    [JsonPropertyName("bodyPart")]
    [JsonRequired]
    public BodyParts BodyPart { get; set; }

    [JsonPropertyName("unit")]
    [JsonRequired]
    public MeasurementUnits Unit { get; set; }
}

public sealed class MeasurementTrendDto : IResultDto
{
    [JsonPropertyName("bodyPart")]
    public EnumLookupDto BodyPart { get; set; } = new();

    [JsonPropertyName("unit")]
    public EnumLookupDto Unit { get; set; } = new();

    [JsonPropertyName("firstMeasurementValue")]
    public double? FirstMeasurementValue { get; set; }

    [JsonPropertyName("firstMeasurementDate")]
    public DateTime? FirstMeasurementDate { get; set; }

    [JsonPropertyName("lastMeasurementValue")]
    public double? LastMeasurementValue { get; set; }

    [JsonPropertyName("lastMeasurementDate")]
    public DateTime? LastMeasurementDate { get; set; }

    [JsonPropertyName("difference")]
    public double? Difference { get; set; }

    [JsonPropertyName("startValue")]
    public double? StartValue { get; set; }

    [JsonPropertyName("currentValue")]
    public double? CurrentValue { get; set; }

    [JsonPropertyName("change")]
    public double? Change { get; set; }

    [JsonPropertyName("changePercentage")]
    public double? ChangePercentage { get; set; }

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = string.Empty;

    [JsonPropertyName("points")]
    public int Points { get; set; }
}
