using System.Text.Json.Serialization;

namespace LgymApi.Api.DTOs;

public sealed class ExerciseScoresChartDataDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("exerciseName")]
    public string ExerciseName { get; set; } = string.Empty;

    [JsonPropertyName("exerciseId")]
    public string ExerciseId { get; set; } = string.Empty;
}

public sealed class ExerciseScoresChartRequestDto
{
    [JsonPropertyName("exerciseId")]
    public string ExerciseId { get; set; } = string.Empty;
}

public class ExerciseScoresFormDto : ExerciseScoresTrainingFormDto
{
    [JsonPropertyName("user")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("training")]
    public string TrainingId { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
}
