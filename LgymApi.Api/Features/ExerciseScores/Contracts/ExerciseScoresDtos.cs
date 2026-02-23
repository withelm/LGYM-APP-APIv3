using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;
using LgymApi.Api.Features.Training.Contracts;

namespace LgymApi.Api.Features.ExerciseScores.Contracts;

public sealed class ExerciseScoresChartDataDto : IResultDto
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

public sealed class ExerciseScoresChartRequestDto : IDto
{
    [JsonPropertyName("exerciseId")]
    public string ExerciseId { get; set; } = string.Empty;
}

public class ExerciseScoresFormDto : ExerciseScoresTrainingFormDto, IResultDto
{
    [JsonPropertyName("user")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("training")]
    public string TrainingId { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
}
