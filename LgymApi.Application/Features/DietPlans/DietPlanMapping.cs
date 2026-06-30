using System.Text.Json;
using LgymApi.Application.Features.DietPlans.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using UserEntity = LgymApi.Domain.Entities.User;

namespace LgymApi.Application.Features.DietPlans;

internal static class DietPlanMapping
{
    public static List<DietMeal> BuildMeals(IEnumerable<UpsertDietMealCommand> meals)
        => meals.Select(meal => new DietMeal
        {
            Id = Id<DietMeal>.New(),
            Name = meal.Name,
            Order = meal.Order,
            Description = meal.Description,
            EstimatedCalories = meal.EstimatedCalories,
            ProteinGrams = meal.ProteinGrams,
            CarbsGrams = meal.CarbsGrams,
            FatGrams = meal.FatGrams
        }).ToList();

    public static void ReplaceMeals(DietPlan plan, IReadOnlyCollection<UpsertDietMealCommand> meals)
    {
        plan.Meals.Clear();
        foreach (var meal in BuildMeals(meals))
        {
            plan.Meals.Add(meal);
        }
    }

    public static DietPlanHistory CreateHistoryEntry(DietPlan plan, Id<UserEntity> changedByUserId, string changeType)
        => new()
        {
            Id = Id<DietPlanHistory>.New(),
            DietPlanId = plan.Id,
            ChangedByUserId = changedByUserId,
            ChangeDate = DateTimeOffset.UtcNow,
            ChangeType = changeType,
            SnapshotJson = JsonSerializer.Serialize(MapPlan(plan))
        };

    public static DietPlanResult MapPlan(DietPlan plan) => new()
    {
        Id = plan.Id,
        TrainerId = plan.TrainerId,
        TraineeId = plan.TraineeId,
        Name = plan.Name,
        StartDate = plan.StartDate,
        EndDate = plan.EndDate,
        EstimatedCalories = plan.EstimatedCalories,
        ProteinGrams = plan.ProteinGrams,
        CarbsGrams = plan.CarbsGrams,
        FatGrams = plan.FatGrams,
        Notes = plan.Notes,
        IsActive = plan.IsActive,
        CreatedAt = plan.CreatedAt,
        UpdatedAt = plan.UpdatedAt,
        Meals = plan.Meals.OrderBy(x => x.Order).Select(MapMeal).ToList()
    };

    public static DietPlanHistoryResult MapHistory(DietPlanHistory entry) => new()
    {
        Id = entry.Id,
        DietPlanId = entry.DietPlanId,
        ChangedByUserId = entry.ChangedByUserId,
        ChangeDate = entry.ChangeDate,
        ChangeType = entry.ChangeType,
        SnapshotJson = entry.SnapshotJson
    };

    private static DietMealResult MapMeal(DietMeal meal) => new()
    {
        Id = meal.Id,
        Name = meal.Name,
        Order = meal.Order,
        Description = meal.Description,
        EstimatedCalories = meal.EstimatedCalories,
        ProteinGrams = meal.ProteinGrams,
        CarbsGrams = meal.CarbsGrams,
        FatGrams = meal.FatGrams
    };
}
