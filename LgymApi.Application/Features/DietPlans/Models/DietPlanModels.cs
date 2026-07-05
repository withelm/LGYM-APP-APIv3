using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.DietPlans.Models;

public sealed class UpsertDietPlanCommand
{
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? EstimatedCalories { get; set; }
    public decimal? ProteinGrams { get; set; }
    public decimal? CarbsGrams { get; set; }
    public decimal? FatGrams { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public List<UpsertDietMealCommand> Meals { get; set; } = [];
}

public sealed class UpsertDietMealCommand
{
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public string? Description { get; set; }
    public int? EstimatedCalories { get; set; }
    public decimal? ProteinGrams { get; set; }
    public decimal? CarbsGrams { get; set; }
    public decimal? FatGrams { get; set; }
}

public sealed class DietPlanResult
{
    public Id<DietPlan> Id { get; set; }
    public Id<UserEntity> TrainerId { get; set; }
    public Id<UserEntity> TraineeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? EstimatedCalories { get; set; }
    public decimal? ProteinGrams { get; set; }
    public decimal? CarbsGrams { get; set; }
    public decimal? FatGrams { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<DietMealResult> Meals { get; set; } = [];
}

public sealed class DietMealResult
{
    public Id<DietMeal> Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public string? Description { get; set; }
    public int? EstimatedCalories { get; set; }
    public decimal? ProteinGrams { get; set; }
    public decimal? CarbsGrams { get; set; }
    public decimal? FatGrams { get; set; }
}

public sealed class DietPlanHistoryResult
{
    public Id<DietPlanHistory> Id { get; set; }
    public Id<DietPlan> DietPlanId { get; set; }
    public Id<UserEntity> ChangedByUserId { get; set; }
    public DateTimeOffset ChangeDate { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
}
