using System.Text.Json.Serialization;
using LgymApi.Api.Features.Enum.Contracts;
using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Api.Features.PlanDay.Contracts;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Api.Interfaces;
using LgymApi.Domain.Enums;

namespace LgymApi.Api.Features.Training.Contracts;

public class ExerciseScoresTrainingFormDto : IResultDto
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("unit")]
    public WeightUnits Unit { get; set; }

    [JsonPropertyName("reps")]
    public int Reps { get; set; }

    [JsonPropertyName("exercise")]
    public string ExerciseId { get; set; } = string.Empty;

    [JsonPropertyName("series")]
    public int Series { get; set; }
}

public sealed class ExerciseScoreResponseDto : IResultDto
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("unit")]
    public EnumLookupDto Unit { get; set; } = new();

    [JsonPropertyName("reps")]
    public int Reps { get; set; }

    [JsonPropertyName("exercise")]
    public string ExerciseId { get; set; } = string.Empty;

    [JsonPropertyName("series")]
    public int Series { get; set; }
}

public sealed class TrainingFormDto : IDto
{
    [JsonPropertyName("type")]
    public string TypePlanDayId { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    [JsonRequired]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("gym")]
    public string GymId { get; set; } = string.Empty;

    [JsonPropertyName("exercises")]
    public List<ExerciseScoresTrainingFormDto> Exercises { get; set; } = new();
}

public sealed class LastTrainingInfoDto : IResultDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string TypePlanDayId { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("planDay")]
    public PlanDayChooseDto PlanDay { get; set; } = new();

    [JsonPropertyName("gym")]
    public string? Gym { get; set; }
}

public sealed class TrainingByDateRequestDto : IDto
{
    [JsonPropertyName("createdAt")]
    [JsonRequired]
    public DateTime CreatedAt { get; set; }
}

public sealed class TrainingExerciseScoreRefDto : IResultDto
{
    [JsonPropertyName("exerciseScoreId")]
    public string ExerciseScoreId { get; set; } = string.Empty;
}

public sealed class TrainingByDateDto : IResultDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string TypePlanDayId { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("planDay")]
    public PlanDayChooseDto PlanDay { get; set; } = new();

    [JsonPropertyName("gym")]
    public string? Gym { get; set; }

    [JsonPropertyName("exercises")]
    public List<TrainingExerciseScoreRefDto> Exercises { get; set; } = new();
}

public sealed class EnrichedExerciseDto : IResultDto
{
    [JsonPropertyName("exerciseScoreId")]
    public string ExerciseScoreId { get; set; } = string.Empty;

    [JsonPropertyName("scoresDetails")]
    public List<ExerciseScoreResponseDto> ScoresDetails { get; set; } = new();

    [JsonPropertyName("exerciseDetails")]
    public ExerciseResponseDto ExerciseDetails { get; set; } = new();
}

public sealed class TrainingByDateDetailsDto : IResultDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string TypePlanDayId { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("planDay")]
    public PlanDayChooseDto PlanDay { get; set; } = new();

    [JsonPropertyName("gym")]
    public string? Gym { get; set; }

    [JsonPropertyName("exercises")]
    public List<EnrichedExerciseDto> Exercises { get; set; } = new();
}

public sealed class SeriesComparisonDto : IResultDto
{
    [JsonPropertyName("series")]
    public int Series { get; set; }

    [JsonPropertyName("currentResult")]
    public ScoreResultDto CurrentResult { get; set; } = new();

    [JsonPropertyName("previousResult")]
    public ScoreResultDto? PreviousResult { get; set; }
}

public sealed class ScoreResultDto : IResultDto
{
    [JsonPropertyName("reps")]
    public int Reps { get; set; }

    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("unit")]
    public EnumLookupDto Unit { get; set; } = new();
}

public sealed class GroupedExerciseComparisonDto : IResultDto
{
    [JsonPropertyName("exerciseId")]
    public string ExerciseId { get; set; } = string.Empty;

    [JsonPropertyName("exerciseName")]
    public string ExerciseName { get; set; } = string.Empty;

    [JsonPropertyName("seriesComparisons")]
    public List<SeriesComparisonDto> SeriesComparisons { get; set; } = new();
}

public sealed class TrainingSummaryDto : IResultDto
{
    [JsonPropertyName("comparison")]
    public List<GroupedExerciseComparisonDto> Comparison { get; set; } = new();

    [JsonPropertyName("gainElo")]
    public int GainElo { get; set; }

    [JsonPropertyName("userOldElo")]
    public int UserOldElo { get; set; }

    [JsonPropertyName("profileRank")]
    public RankDto? ProfileRank { get; set; }

    [JsonPropertyName("nextRank")]
    public RankDto? NextRank { get; set; }

    [JsonPropertyName("msg")]
    public string Message { get; set; } = string.Empty;
}
