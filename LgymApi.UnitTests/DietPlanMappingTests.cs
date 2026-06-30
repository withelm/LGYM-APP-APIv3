using System.Text.Json;
using FluentAssertions;
using LgymApi.Application.Features.DietPlans;
using LgymApi.Application.Features.DietPlans.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class DietPlanMappingTests
{
    [Test]
    public void BuildMeals_MapsCommandsIntoOrderedMealEntities()
    {
        var commands = new[]
        {
            new UpsertDietMealCommand
            {
                Name = "Breakfast",
                Order = 2,
                Description = "Eggs",
                EstimatedCalories = 500,
                ProteinGrams = 30,
                CarbsGrams = 20,
                FatGrams = 15
            }
        };

        var meals = DietPlanMapping.BuildMeals(commands);

        meals.Should().ContainSingle();
        meals[0].Name.Should().Be("Breakfast");
        meals[0].Order.Should().Be(2);
        meals[0].Description.Should().Be("Eggs");
        meals[0].EstimatedCalories.Should().Be(500);
    }

    [Test]
    public void ReplaceMeals_ReplacesExistingMealsWithMappedValues()
    {
        var plan = new DietPlan
        {
            Meals =
            [
                new DietMeal { Id = Id<DietMeal>.New(), Name = "Old", Order = 0 }
            ]
        };

        DietPlanMapping.ReplaceMeals(plan,
        [
            new UpsertDietMealCommand { Name = "Lunch", Order = 1, Description = "Rice" },
            new UpsertDietMealCommand { Name = "Dinner", Order = 2, Description = "Fish" }
        ]);

        plan.Meals.Select(x => x.Name).Should().Equal("Lunch", "Dinner");
        plan.Meals.All(x => x.Id != default).Should().BeTrue();
    }

    [Test]
    public void MapPlan_AndCreateHistoryEntry_PreservePlanState()
    {
        var trainerId = Id<User>.New();
        var traineeId = Id<User>.New();
        var plan = new DietPlan
        {
            Id = Id<DietPlan>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId,
            Name = "Lean Bulk",
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 7, 1),
            EstimatedCalories = 2800,
            ProteinGrams = 180,
            CarbsGrams = 300,
            FatGrams = 70,
            Notes = "Track weekly",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero),
            Meals =
            [
                new DietMeal { Id = Id<DietMeal>.New(), Name = "Dinner", Order = 2 },
                new DietMeal { Id = Id<DietMeal>.New(), Name = "Breakfast", Order = 1 }
            ]
        };

        var mapped = DietPlanMapping.MapPlan(plan);
        var history = DietPlanMapping.CreateHistoryEntry(plan, trainerId, "Updated");
        var snapshot = JsonSerializer.Deserialize<DietPlanResult>(history.SnapshotJson);

        mapped.Name.Should().Be("Lean Bulk");
        mapped.Meals.Select(x => x.Name).Should().Equal("Breakfast", "Dinner");
        history.DietPlanId.Should().Be(plan.Id);
        history.ChangedByUserId.Should().Be(trainerId);
        history.ChangeType.Should().Be("Updated");
        snapshot.Should().NotBeNull();
        snapshot!.Name.Should().Be(mapped.Name);
        snapshot.Meals.Select(x => x.Name).Should().Equal(mapped.Meals.Select(x => x.Name));
    }

    [Test]
    public void MapHistory_MapsStoredHistoryValues()
    {
        var history = new DietPlanHistory
        {
            Id = Id<DietPlanHistory>.New(),
            DietPlanId = Id<DietPlan>.New(),
            ChangedByUserId = Id<User>.New(),
            ChangeDate = DateTimeOffset.UtcNow,
            ChangeType = "Created",
            SnapshotJson = "{}"
        };

        var mapped = DietPlanMapping.MapHistory(history);

        mapped.Id.Should().Be(history.Id);
        mapped.DietPlanId.Should().Be(history.DietPlanId);
        mapped.ChangedByUserId.Should().Be(history.ChangedByUserId);
        mapped.ChangeType.Should().Be("Created");
        mapped.SnapshotJson.Should().Be("{}");
    }
}
