using System.Text.Json.Serialization;

namespace LgymApi.Api.DTOs;

public class MainRecordsFormDto
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("exercise")]
    public string ExerciseId { get; set; } = string.Empty;
}

public class MainRecordsLastDto : MainRecordsFormDto
{
    [JsonPropertyName("exerciseDetails")]
    public ExerciseFormDto ExerciseDetails { get; set; } = new();
}

public sealed class PossibleRecordForExerciseDto
{
    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("reps")]
    public int Reps { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
}

public sealed class RecordOrPossibleRequestDto
{
    [JsonPropertyName("exerciseId")]
    public string ExerciseId { get; set; } = string.Empty;
}
