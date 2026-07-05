using LgymApi.Domain.ValueObjects;

namespace LgymApi.Domain.Entities;

public sealed class DietPlan : EntityBase<DietPlan>
{
    public Id<User> TrainerId { get; set; }
    public Id<User> TraineeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? EstimatedCalories { get; set; }
    public decimal? ProteinGrams { get; set; }
    public decimal? CarbsGrams { get; set; }
    public decimal? FatGrams { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }

    public User Trainer { get; set; } = null!;
    public User Trainee { get; set; } = null!;
    public ICollection<DietMeal> Meals { get; set; } = new List<DietMeal>();
    public ICollection<DietPlanHistory> HistoryEntries { get; set; } = new List<DietPlanHistory>();
}
