using System.Text.Json.Serialization;

namespace LgymApi.Api.DTOs;

public class ExerciseScoresTrainingFormDto
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("reps")]
    public int Reps { get; set; }

    [JsonPropertyName("exercise")]
    public string ExerciseId { get; set; } = string.Empty;

    [JsonPropertyName("series")]
    public int Series { get; set; }
}

public sealed class ExerciseScoreResponseDto
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

public sealed class TrainingFormDto
{
    [JsonPropertyName("type")]
    public string TypePlanDayId { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("gym")]
    public string GymId { get; set; } = string.Empty;

    [JsonPropertyName("exercises")]
    public List<ExerciseScoresTrainingFormDto> Exercises { get; set; } = new();
}

public class LastTrainingInfoDto
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

public sealed class TrainingByDateRequestDto
{
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public sealed class TrainingExerciseScoreRefDto
{
    [JsonPropertyName("exerciseScoreId")]
    public string ExerciseScoreId { get; set; } = string.Empty;
}

public class TrainingByDateDto : LastTrainingInfoDto
{
    [JsonPropertyName("exercises")]
    public List<TrainingExerciseScoreRefDto> Exercises { get; set; } = new();
}

public sealed class EnrichedExerciseDto
{
    [JsonPropertyName("exerciseScoreId")]
    public string ExerciseScoreId { get; set; } = string.Empty;

    [JsonPropertyName("scoresDetails")]
    public List<ExerciseScoreResponseDto> ScoresDetails { get; set; } = new();

    [JsonPropertyName("exerciseDetails")]
    public ExerciseResponseDto ExerciseDetails { get; set; } = new();
}

public class TrainingByDateDetailsDto : LastTrainingInfoDto
{
    [JsonPropertyName("exercises")]
    public List<EnrichedExerciseDto> Exercises { get; set; } = new();
}

public sealed class SeriesComparisonDto
{
    [JsonPropertyName("series")]
    public int Series { get; set; }

    [JsonPropertyName("currentResult")]
    public ScoreResultDto CurrentResult { get; set; } = new();

    [JsonPropertyName("previousResult")]
    public ScoreResultDto? PreviousResult { get; set; }
}

public sealed class ScoreResultDto
{
    [JsonPropertyName("reps")]
    public int Reps { get; set; }

    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("unit")]
    public EnumLookupDto Unit { get; set; } = new();
}

public sealed class GroupedExerciseComparisonDto
{
    [JsonPropertyName("exerciseId")]
    public string ExerciseId { get; set; } = string.Empty;

    [JsonPropertyName("exerciseName")]
    public string ExerciseName { get; set; } = string.Empty;

    [JsonPropertyName("seriesComparisons")]
    public List<SeriesComparisonDto> SeriesComparisons { get; set; } = new();
}

public sealed class TrainingSummaryDto
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
