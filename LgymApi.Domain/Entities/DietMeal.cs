using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class DietMeal : EntityBase<DietMeal>
{
    public Id<DietPlan> DietPlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public string? Description { get; set; }
    public int? EstimatedCalories { get; set; }
    public decimal? ProteinGrams { get; set; }
    public decimal? CarbsGrams { get; set; }
    public decimal? FatGrams { get; set; }

    public DietPlan DietPlan { get; set; } = null!;
}
