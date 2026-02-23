using System.Text.Json.Serialization;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.PlanDay.Contracts;

public sealed class PlanDayExerciseInputDto
{
    [JsonPropertyName("series")]
    public int Series { get; set; }

    [JsonPropertyName("reps")]
    public string Reps { get; set; } = string.Empty;

    [JsonPropertyName("exercise")]
    public string ExerciseId { get; set; } = string.Empty;
}

public sealed class PlanDayFormDto : IDto
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("exercises")]
    public List<PlanDayExerciseInputDto> Exercises { get; set; } = new();
}

public sealed class PlanDayExerciseVmDto
{
    [JsonPropertyName("series")]
    public int Series { get; set; }

    [JsonPropertyName("reps")]
    public string Reps { get; set; } = string.Empty;

    [JsonPropertyName("exercise")]
    public ExerciseResponseDto Exercise { get; set; } = new();
}

public sealed class PlanDayVmDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("exercises")]
    public List<PlanDayExerciseVmDto> Exercises { get; set; } = new();
}

public sealed class PlanDayChooseDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class PlanDayBaseInfoDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("lastTrainingDate")]
    public DateTime? LastTrainingDate { get; set; }

    [JsonPropertyName("totalNumberOfSeries")]
    public int TotalNumberOfSeries { get; set; }

    [JsonPropertyName("totalNumberOfExercises")]
    public int TotalNumberOfExercises { get; set; }
}
