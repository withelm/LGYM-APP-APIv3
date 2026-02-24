using System.Text.Json.Serialization;
using LgymApi.Api.Features.Enum.Contracts;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Api.Interfaces;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Features.MainRecords.Contracts;

public sealed class MainRecordsFormDto : IDto
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("weight")]
    [JsonRequired]
    public double Weight { get; set; }

    [JsonPropertyName("date")]
    [JsonRequired]
    public DateTime Date { get; set; }

    [JsonPropertyName("unit")]
    [JsonRequired]
    public WeightUnits Unit { get; set; }

    [JsonPropertyName("exercise")]
    public string ExerciseId { get; set; } = string.Empty;
}

public sealed class MainRecordResponseDto : IResultDto
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("unit")]
    public EnumLookupDto Unit { get; set; } = new();

    [JsonPropertyName("exercise")]
    public string ExerciseId { get; set; } = string.Empty;
}

public sealed class MainRecordsLastDto : IResultDto
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("unit")]
    public EnumLookupDto Unit { get; set; } = new();

    [JsonPropertyName("exercise")]
    public string ExerciseId { get; set; } = string.Empty;

    [JsonPropertyName("exerciseDetails")]
    public ExerciseResponseDto ExerciseDetails { get; set; } = new();
}

public sealed class PossibleRecordForExerciseDto : IResultDto
{
    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("reps")]
    public int Reps { get; set; }

    [JsonPropertyName("unit")]
    public EnumLookupDto Unit { get; set; } = new();

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
}

public sealed class RecordOrPossibleRequestDto : IDto
{
    [JsonPropertyName("exerciseId")]
    public string ExerciseId { get; set; } = string.Empty;
}
