using System.Text.Json;
using FluentAssertions;
using LgymApi.Api.Features.Trainer.Contracts;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class DietPlanDtosTests
{
    [Test]
    public void UpsertDietPlanRequest_RoundTripsInheritedProperties()
    {
        var request = new UpsertDietPlanRequest
        {
            Name = "Cut",
            StartDate = new DateOnly(2026, 7, 1),
            EndDate = new DateOnly(2026, 8, 1),
            EstimatedCalories = 2200,
            ProteinGrams = 180,
            CarbsGrams = 150,
            FatGrams = 60,
            Notes = "Keep deficit",
            IsActive = true,
            Meals =
            [
                new UpsertDietMealRequest
                {
                    Name = "Breakfast",
                    Order = 1,
                    Description = "Oats",
                    EstimatedCalories = 500,
                    ProteinGrams = 30,
                    CarbsGrams = 70,
                    FatGrams = 10
                }
            ]
        };

        var json = JsonSerializer.Serialize(request);
        var roundTrip = JsonSerializer.Deserialize<UpsertDietPlanRequest>(json);

        roundTrip.Should().NotBeNull();
        roundTrip!.Name.Should().Be("Cut");
        roundTrip.EstimatedCalories.Should().Be(2200);
        roundTrip.ProteinGrams.Should().Be(180);
        roundTrip.Meals.Should().ContainSingle();
        roundTrip.Meals[0].Description.Should().Be("Oats");
        roundTrip.Meals[0].FatGrams.Should().Be(10);
    }

    [Test]
    public void DietPlanDto_RoundTripsResultAndNestedMeals()
    {
        var dto = new DietPlanDto
        {
            Id = "plan-1",
            TrainerId = "trainer-1",
            TraineeId = "trainee-1",
            Name = "Lean bulk",
            StartDate = new DateOnly(2026, 7, 1),
            EndDate = new DateOnly(2026, 7, 31),
            EstimatedCalories = 2900,
            ProteinGrams = 190,
            CarbsGrams = 320,
            FatGrams = 75,
            Notes = "Weekly check-in",
            IsActive = true,
            CreatedAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 7, 2, 0, 0, 0, TimeSpan.Zero),
            Meals =
            [
                new DietMealDto
                {
                    Id = "meal-1",
                    Name = "Dinner",
                    Order = 2,
                    Description = "Rice and chicken",
                    EstimatedCalories = 900,
                    ProteinGrams = 55,
                    CarbsGrams = 100,
                    FatGrams = 20
                }
            ]
        };

        var json = JsonSerializer.Serialize(dto);
        var roundTrip = JsonSerializer.Deserialize<DietPlanDto>(json);

        roundTrip.Should().NotBeNull();
        roundTrip!.TrainerId.Should().Be("trainer-1");
        roundTrip.TraineeId.Should().Be("trainee-1");
        roundTrip.Name.Should().Be("Lean bulk");
        roundTrip.Meals.Should().ContainSingle();
        roundTrip.Meals[0].Id.Should().Be("meal-1");
        roundTrip.Meals[0].EstimatedCalories.Should().Be(900);
    }
}
