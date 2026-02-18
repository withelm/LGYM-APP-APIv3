using System.Text.Json.Serialization;
using LgymApi.Api.Features.Enum.Contracts;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Api.Serialization;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Features.MainRecords.Contracts;

public class MainRecordsFormDto
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("unit")]
    [JsonRequired]
    [JsonConverter(typeof(WeightUnitsJsonConverter))]
    public WeightUnits Unit { get; set; }

    [JsonPropertyName("exercise")]
    public string ExerciseId { get; set; } = string.Empty;
}

public class MainRecordResponseDto
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

public class MainRecordsLastDto : MainRecordResponseDto
{
    [JsonPropertyName("exerciseDetails")]
    public ExerciseResponseDto ExerciseDetails { get; set; } = new();
}

public sealed class PossibleRecordForExerciseDto
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

public sealed class RecordOrPossibleRequestDto
{
    [JsonPropertyName("exerciseId")]
    public string ExerciseId { get; set; } = string.Empty;
}
