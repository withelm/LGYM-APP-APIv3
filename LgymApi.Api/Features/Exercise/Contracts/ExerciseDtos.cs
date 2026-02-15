using System.Text.Json.Serialization;
using LgymApi.Api.Features.Enum.Contracts;

namespace LgymApi.Api.Features.Exercise.Contracts;

public sealed class ExerciseFormDto
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("user")]
    public string? UserId { get; set; }

    [JsonPropertyName("bodyPart")]
    public string BodyPart { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }
}

public sealed class ExerciseResponseDto
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("user")]
    public string? UserId { get; set; }

    [JsonPropertyName("bodyPart")]
    public EnumLookupDto BodyPart { get; set; } = new();

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }
}

public sealed class ExerciseTranslationDto
{
    [JsonPropertyName("exerciseId")]
    public string ExerciseId { get; set; } = string.Empty;

    [JsonPropertyName("culture")]
    public string Culture { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class SeriesScoreDto
{
    [JsonPropertyName("series")]
    public int Series { get; set; }

    [JsonPropertyName("score")]
    public ScoreDto? Score { get; set; }
}

public class ScoreDto
{
    [JsonPropertyName("reps")]
    public int Reps { get; set; }

    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("unit")]
    public EnumLookupDto Unit { get; set; } = new();

    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;
}

public class ScoreWithGymDto : ScoreDto
{
    [JsonPropertyName("gymName")]
    public string? GymName { get; set; }
}

public sealed class SeriesScoreWithGymDto
{
    [JsonPropertyName("series")]
    public int Series { get; set; }

    [JsonPropertyName("score")]
    public ScoreWithGymDto? Score { get; set; }
}

public sealed class LastExerciseScoresRequestDto
{
    [JsonPropertyName("series")]
    public int Series { get; set; }

    [JsonPropertyName("exerciseId")]
    public string ExerciseId { get; set; } = string.Empty;

    [JsonPropertyName("gym")]
    public string? GymId { get; set; }

    [JsonPropertyName("exerciseName")]
    public string ExerciseName { get; set; } = string.Empty;
}

public sealed class LastExerciseScoresResponseDto
{
    [JsonPropertyName("exerciseId")]
    public string ExerciseId { get; set; } = string.Empty;

    [JsonPropertyName("exerciseName")]
    public string ExerciseName { get; set; } = string.Empty;

    [JsonPropertyName("seriesScores")]
    public List<SeriesScoreWithGymDto> SeriesScores { get; set; } = new();
}

public sealed class ExerciseTrainingHistoryItemDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("gymName")]
    public string GymName { get; set; } = string.Empty;

    [JsonPropertyName("trainingName")]
    public string TrainingName { get; set; } = string.Empty;

    [JsonPropertyName("seriesScores")]
    public List<SeriesScoreDto> SeriesScores { get; set; } = new();
}
