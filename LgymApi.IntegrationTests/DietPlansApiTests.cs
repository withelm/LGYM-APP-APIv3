using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class DietPlansApiTests : IntegrationTestBase
{
    [Test]
    public async Task TrainerDietPlanCrudFlow_Works()
    {
        var trainer = await SeedTrainerAsync("trainer-diet", "trainer-diet@example.com");
        var trainee = await SeedUserAsync(name: "trainee-diet", email: "trainee-diet@example.com", password: "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        var createResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans", new
        {
            name = "Mass phase",
            startDate = new DateOnly(2026, 6, 1),
            estimatedCalories = 3100,
            proteinGrams = 180,
            carbsGrams = 360,
            fatGrams = 90,
            notes = "Initial version",
            isActive = true,
            meals = new object[]
            {
                new { name = "Breakfast", order = 0, description = "Eggs and oats", estimatedCalories = 750, proteinGrams = 40, carbsGrams = 60, fatGrams = 25 }
            }
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<DietPlanResponse>();
        created.Should().NotBeNull();
        created!.IsActive.Should().BeTrue();

        var listResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var plans = await listResponse.Content.ReadFromJsonAsync<List<DietPlanResponse>>();
        plans.Should().ContainSingle(x => x.Id == created.Id && x.IsActive);

        var updateResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{created.Id}/update", new
        {
            name = "Mass phase v2",
            startDate = new DateOnly(2026, 6, 2),
            endDate = new DateOnly(2026, 8, 31),
            estimatedCalories = 3200,
            proteinGrams = 190,
            carbsGrams = 375,
            fatGrams = 92,
            notes = "Updated version",
            isActive = true,
            meals = new object[]
            {
                new { name = "Breakfast", order = 0, description = "Eggs, oats, banana", estimatedCalories = 800, proteinGrams = 45, carbsGrams = 70, fatGrams = 25 },
                new { name = "Dinner", order = 1, description = "Rice and chicken", estimatedCalories = 900, proteinGrams = 55, carbsGrams = 100, fatGrams = 20 }
            }
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var historyResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{created.Id}/history");
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await historyResponse.Content.ReadFromJsonAsync<List<DietPlanHistoryResponse>>();
        history.Should().NotBeNull();
        history!.Count.Should().BeGreaterThanOrEqualTo(2);
        history.Select(x => x.ChangeType).Should().Contain(new[] { "Created", "Updated" });

        SetAuthorizationHeader(trainee.Id);
        var currentResponse = await Client.GetAsync("/api/trainee/diet-plan/current");
        currentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var current = await currentResponse.Content.ReadFromJsonAsync<DietPlanResponse>();
        current.Should().NotBeNull();
        current!.Name.Should().Be("Mass phase v2");
        current.Meals.Should().HaveCount(2);
    }

    [Test]
    public async Task TrainerCannotCreateDietForForeignTrainee()
    {
        var ownerTrainer = await SeedTrainerAsync("trainer-owner-diet", "trainer-owner-diet@example.com");
        var otherTrainer = await SeedTrainerAsync("trainer-other-diet", "trainer-other-diet@example.com");
        var trainee = await SeedUserAsync(name: "trainee-foreign-diet", email: "trainee-foreign-diet@example.com", password: "password123");
        await LinkTrainerAndTraineeAsync(ownerTrainer.Id, trainee.Id);

        SetAuthorizationHeader(otherTrainer.Id);
        var response = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans", new
        {
            name = "Forbidden",
            startDate = new DateOnly(2026, 6, 1),
            isActive = false,
            meals = new object[] { new { name = "Meal", order = 0 } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ActivateDietPlan_DeactivatesPreviousActivePlan()
    {
        var trainer = await SeedTrainerAsync("trainer-activate-diet", "trainer-activate-diet@example.com");
        var trainee = await SeedUserAsync(name: "trainee-activate-diet", email: "trainee-activate-diet@example.com", password: "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        var first = await CreateDietAsync(trainee.Id, "Diet A", true);
        var second = await CreateDietAsync(trainee.Id, "Diet B", false);

        var activateResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{second.Id}/activate", null);
        activateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans");
        var plans = await listResponse.Content.ReadFromJsonAsync<List<DietPlanResponse>>();
        plans.Should().Contain(x => x.IsActive && x.Id == second.Id);
        plans.Should().Contain(x => x.IsActive && x.Id == first.Id);
    }

    [Test]
    public async Task TrainerCanCreateGlobalDietWithoutMeals_WhenMacroTargetsAreProvided()
    {
        var trainer = await SeedTrainerAsync("trainer-global-diet", "trainer-global-diet@example.com");
        var trainee = await SeedUserAsync(name: "trainee-global-diet", email: "trainee-global-diet@example.com", password: "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        var createResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans", new
        {
            name = "Rest day",
            startDate = new DateOnly(2026, 6, 24),
            estimatedCalories = 450,
            proteinGrams = 50,
            carbsGrams = 40,
            fatGrams = 10,
            notes = "Global only",
            isActive = true,
            meals = Array.Empty<object>()
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<DietPlanResponse>();
        created.Should().NotBeNull();
        created!.Meals.Should().BeEmpty();
        created.IsActive.Should().BeTrue();
    }

    [Test]
    public async Task TraineeCurrentDiets_ReturnsAllActiveDiets()
    {
        var trainer = await SeedTrainerAsync("trainer-current-diets", "trainer-current-diets@example.com");
        var trainee = await SeedUserAsync(name: "trainee-current-diets", email: "trainee-current-diets@example.com", password: "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        var trainingDay = await CreateDietAsync(trainee.Id, "Training Day", true);
        var restDay = await CreateDietAsync(trainee.Id, "Rest Day", true);

        SetAuthorizationHeader(trainee.Id);
        var currentResponse = await Client.GetAsync("/api/trainee/diet-plans/current");
        currentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var currentPlans = await currentResponse.Content.ReadFromJsonAsync<List<DietPlanResponse>>();
        currentPlans.Should().NotBeNull();
        currentPlans!.Select(x => x.Id).Should().Contain(new[] { trainingDay.Id, restDay.Id });
        currentPlans.Should().OnlyContain(x => x.IsActive);
    }

    [Test]
    public async Task ActiveDietUpdate_QueuesDietNotificationCommand()
    {
        var trainer = await SeedTrainerAsync("trainer-diet-notif", "trainer-diet-notif@example.com");
        var trainee = await SeedUserAsync(name: "trainee-diet-notif", email: "trainee-diet-notif@example.com", password: "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        var plan = await CreateDietAsync(trainee.Id, "Diet Notification", true);

        var response = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/update", new
        {
            name = "Diet Notification v2",
            startDate = new DateOnly(2026, 6, 5),
            isActive = true,
            meals = new object[] { new { name = "Meal", order = 0, estimatedCalories = 500 } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationCommand = await db.CommandEnvelopes
            .Where(x => x.CommandTypeFullName.Contains("DietPlanUpdatedInAppNotificationCommand"))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        notificationCommand.Should().NotBeNull();
        notificationCommand!.PayloadJson.Should().Contain("Diet Notification v2");
        notificationCommand.PayloadJson.Should().Contain(trainee.Id.ToString());
    }

    private async Task<DietPlanResponse> CreateDietAsync(Id<User> traineeId, string name, bool isActive)
    {
        var response = await Client.PostAsJsonAsync($"/api/trainer/trainees/{traineeId}/diet-plans", new
        {
            name,
            startDate = new DateOnly(2026, 6, 1),
            isActive,
            meals = new object[] { new { name = "Meal", order = 0, estimatedCalories = 400 } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<DietPlanResponse>())!;
    }

    private async Task<User> SeedTrainerAsync(string name, string email)
    {
        var trainer = await SeedUserAsync(name: name, email: email, password: "password123");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alreadyLinked = await db.UserRoles.AnyAsync(ur => ur.UserId == trainer.Id && ur.RoleId == RoleSeedDataConfiguration.TrainerRoleSeedId);
        if (!alreadyLinked)
        {
            db.UserRoles.Add(new UserRole
            {
                UserId = trainer.Id,
                RoleId = RoleSeedDataConfiguration.TrainerRoleSeedId
            });
            await db.SaveChangesAsync();
        }

        return trainer;
    }

    private async Task LinkTrainerAndTraineeAsync(Id<User> trainerId, Id<User> traineeId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.TrainerTraineeLinks.Add(new TrainerTraineeLink
        {
            Id = Id<TrainerTraineeLink>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId
        });
        await db.SaveChangesAsync();
    }

    private sealed class DietPlanResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        [JsonPropertyName("meals")]
        public List<DietMealResponse> Meals { get; set; } = [];
    }

    private sealed class DietMealResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class DietPlanHistoryResponse
    {
        [JsonPropertyName("changeType")]
        public string ChangeType { get; set; } = string.Empty;
    }
}
