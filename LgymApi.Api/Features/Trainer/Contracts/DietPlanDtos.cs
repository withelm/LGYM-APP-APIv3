using System.Text.Json.Serialization;
using LgymApi.Api.Interfaces;

namespace LgymApi.Api.Features.Trainer.Contracts;

public sealed class UpsertDietPlanRequest : IDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("startDate")]
    public DateOnly StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateOnly? EndDate { get; set; }

    [JsonPropertyName("estimatedCalories")]
    public int? EstimatedCalories { get; set; }

    [JsonPropertyName("proteinGrams")]
    public decimal? ProteinGrams { get; set; }

    [JsonPropertyName("carbsGrams")]
    public decimal? CarbsGrams { get; set; }

    [JsonPropertyName("fatGrams")]
    public decimal? FatGrams { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("meals")]
    public List<UpsertDietMealRequest> Meals { get; set; } = [];
}

public sealed class UpsertDietMealRequest : IDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("estimatedCalories")]
    public int? EstimatedCalories { get; set; }

    [JsonPropertyName("proteinGrams")]
    public decimal? ProteinGrams { get; set; }

    [JsonPropertyName("carbsGrams")]
    public decimal? CarbsGrams { get; set; }

    [JsonPropertyName("fatGrams")]
    public decimal? FatGrams { get; set; }
}

public sealed class DietPlanDto : IResultDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("trainerId")]
    public string TrainerId { get; set; } = string.Empty;

    [JsonPropertyName("traineeId")]
    public string TraineeId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("startDate")]
    public DateOnly StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateOnly? EndDate { get; set; }

    [JsonPropertyName("estimatedCalories")]
    public int? EstimatedCalories { get; set; }

    [JsonPropertyName("proteinGrams")]
    public decimal? ProteinGrams { get; set; }

    [JsonPropertyName("carbsGrams")]
    public decimal? CarbsGrams { get; set; }

    [JsonPropertyName("fatGrams")]
    public decimal? FatGrams { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("meals")]
    public List<DietMealDto> Meals { get; set; } = [];
}

public sealed class DietMealDto : IResultDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("estimatedCalories")]
    public int? EstimatedCalories { get; set; }

    [JsonPropertyName("proteinGrams")]
    public decimal? ProteinGrams { get; set; }

    [JsonPropertyName("carbsGrams")]
    public decimal? CarbsGrams { get; set; }

    [JsonPropertyName("fatGrams")]
    public decimal? FatGrams { get; set; }
}

public sealed class DietPlanHistoryDto : IResultDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("dietPlanId")]
    public string DietPlanId { get; set; } = string.Empty;

    [JsonPropertyName("changedByUserId")]
    public string ChangedByUserId { get; set; } = string.Empty;

    [JsonPropertyName("changeDate")]
    public DateTimeOffset ChangeDate { get; set; }

    [JsonPropertyName("changeType")]
    public string ChangeType { get; set; } = string.Empty;

    [JsonPropertyName("snapshotJson")]
    public string SnapshotJson { get; set; } = string.Empty;
}
