using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class GymTests : IntegrationTestBase
{
    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/gym/{id}/addGym", "own", "owner-allow")]
    public async Task AddGym_WithValidData_CreatesGym()
    {
        var user = await SeedUserAsync(name: "gymuser", email: "gym@example.com");
        SetAuthorizationHeader(user.Id);

        var request = new
        {
            name = "Test Gym",
            address = (string?)null
        };

        var response = await Client.PostAsJsonAsync($"/api/gym/{user.Id}/addGym", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("Created"));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var gym = await db.Gyms.FirstOrDefaultAsync(g => g.Name == "Test Gym" && g.UserId == user.Id);
        Assert.That(gym, Is.Not.Null);
        Assert.That(gym!.IsDeleted, Is.False);
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/gym/{id}/addGym", "own", "foreign-object-denial-no-mutation")]
    public async Task AddGym_WithMismatchedUserId_ReturnsForbidden()
    {
        var user = await SeedUserAsync(name: "user1", email: "user1@example.com");
        var otherUser = await SeedUserAsync(name: "user2", email: "user2@example.com");
        SetAuthorizationHeader(user.Id);

        var request = new
        {
            name = "Test Gym"
        };

        var response = await Client.PostAsJsonAsync($"/api/gym/{otherUser.Id}/addGym", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task AddGym_WithoutName_ReturnsBadRequest()
    {
        var user = await SeedUserAsync(name: "gymuser", email: "gym@example.com");
        SetAuthorizationHeader(user.Id);

        var request = new
        {
            name = ""
        };

        var response = await Client.PostAsJsonAsync($"/api/gym/{user.Id}/addGym", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/gym/{id}/deleteGym", "own", "owner-allow")]
    public async Task DeleteGym_WithValidGym_MarkAsDeleted()
    {
        var user = await SeedUserAsync(name: "gymuser", email: "gym@example.com");
        var gym = await SeedGymAsync(user.Id, "Gym to Delete");
        SetAuthorizationHeader(user.Id);

        var response = await Client.PostAsJsonAsync($"/api/gym/{gym.Id}/deleteGym", new { });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var updatedGym = await db.Gyms
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.Id == gym.Id);
        Assert.That(updatedGym, Is.Not.Null);
        Assert.That(updatedGym!.IsDeleted, Is.True);
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/gym/{id}/deleteGym", "own", "foreign-object-denial-no-mutation")]
    public async Task DeleteGym_WithOtherUsersGym_ReturnsForbidden()
    {
        var user1 = await SeedUserAsync(name: "user1", email: "user1@example.com");
        var user2 = await SeedUserAsync(name: "user2", email: "user2@example.com");
        var gym = await SeedGymAsync(user2.Id, "User 2 Gym");
        SetAuthorizationHeader(user1.Id);

        var response = await Client.PostAsJsonAsync($"/api/gym/{gym.Id}/deleteGym", new { });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task GetGyms_WithNoGyms_ReturnsEmptyList()
    {
        var user = await SeedUserAsync(name: "gymuser", email: "gym@example.com");
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync($"/api/gym/{user.Id}/getGyms");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<List<GymChoiceInfoResponse>>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body, Is.Empty);
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/gym/{id}/getGyms", "own", "owner-allow")]
    public async Task GetGyms_WithMultipleGyms_ReturnsAllUserGyms()
    {
        var user = await SeedUserAsync(name: "gymuser", email: "gym@example.com");
        await SeedGymAsync(user.Id, "Gym 1");
        await SeedGymAsync(user.Id, "Gym 2");
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync($"/api/gym/{user.Id}/getGyms");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<List<GymChoiceInfoResponse>>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body, Has.Exactly(2).Items);
        Assert.That(body!.Select(g => g.Name), Is.SupersetOf(new[] { "Gym 1", "Gym 2" }));
    }

    [Test]
    public async Task GetGyms_WithTrainingHistory_ReturnsLastTrainingNestedInfo()
    {
        var (userId, _) = await RegisterUserViaEndpointAsync(
            name: "gymhistoryuser",
            email: "gymhistory@example.com",
            password: "password123");

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Gym History Exercise", BodyParts.Back);
        var gymId = await CreateGymViaEndpointAsync(userId, "History Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "History Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "History Pull Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 3, Reps = "10" }
        });

        var trainingRequest = new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = DateTime.UtcNow,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 10, weight = 50.0, unit = WeightUnits.Kilograms.ToString() }
            }
        };
        await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", trainingRequest);

        var response = await Client.GetAsync($"/api/gym/{userId}/getGyms");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<List<GymChoiceInfoResponse>>();
        Assert.That(body, Is.Not.Null);

        var gym = body!.Single(g => g.Id == gymId.ToString());
        Assert.That(gym.LastTrainingInfo, Is.Not.Null);
        Assert.That(gym.LastTrainingInfo!.Type, Is.Not.Null);
        Assert.That(gym.LastTrainingInfo.Type!.Id, Is.EqualTo(planDayId.ToString()));
        Assert.That(gym.LastTrainingInfo.Type.Name, Is.EqualTo("History Pull Day"));
    }

    [Test]
    public async Task GetGyms_LegacyForeignTrainingDoesNotDiscloseLastTrainingInfo()
    {
        var owner = await SeedUserAsync(name: "gym-legacy-owner", email: "gym-legacy-owner@example.com");
        var foreignOwner = await SeedUserAsync(name: "gym-legacy-foreign", email: "gym-legacy-foreign@example.com");
        var gym = await SeedGymAsync(owner.Id, "Legacy Shared Gym");
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.Trainings.Add(new Training
            {
                Id = Id<Training>.New(),
                UserId = foreignOwner.Id,
                TypePlanDayId = Id<PlanDay>.New(),
                GymId = gym.Id,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await database.SaveChangesAsync();
        }

        SetAuthorizationHeader(owner.Id);
        var response = await Client.GetAsync($"/api/gym/{owner.Id}/getGyms");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<List<GymChoiceInfoResponse>>();
        Assert.That(body, Has.Exactly(1).Items);
        Assert.That(body!.Single().LastTrainingInfo, Is.Null);
    }

    [Test]
    public async Task GetGyms_WhenLatestTrainingsSharePlanDay_ReturnsBothGyms()
    {
        var (userId, _) = await RegisterUserViaEndpointAsync(
            name: "sharedplandayuser",
            email: "sharedplanday@example.com",
            password: "password123");

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Shared Plan Day Exercise", BodyParts.Back);
        var firstGymId = await CreateGymViaEndpointAsync(userId, "First Shared Plan Day Gym");
        var secondGymId = await CreateGymViaEndpointAsync(userId, "Second Shared Plan Day Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "Shared Plan Day Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Shared Plan Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 3, Reps = "10" }
        });
        var firstTraining = new
        {
            gym = firstGymId.ToString(),
            type = planDayId.ToString(),
            createdAt = DateTime.UtcNow.AddMinutes(-1),
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 10, weight = 50.0, unit = WeightUnits.Kilograms.ToString() }
            }
        };
        var secondTraining = new
        {
            gym = secondGymId.ToString(),
            type = planDayId.ToString(),
            createdAt = DateTime.UtcNow,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 10, weight = 55.0, unit = WeightUnits.Kilograms.ToString() }
            }
        };
        await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", firstTraining);
        await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", secondTraining);

        var response = await Client.GetAsync($"/api/gym/{userId}/getGyms");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<List<GymChoiceInfoResponse>>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body, Has.Exactly(2).Items);
        Assert.That((body!.Select(gym => gym.LastTrainingInfo?.Type?.Id)).All(id => id == planDayId.ToString()), Is.True);
    }

    [Test]
    public async Task GetGyms_ExcludesDeletedGyms()
    {
        var user = await SeedUserAsync(name: "gymuser", email: "gym@example.com");
        await SeedGymAsync(user.Id, "Active Gym");
        await SeedGymAsync(user.Id, "Deleted Gym", isDeleted: true);
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync($"/api/gym/{user.Id}/getGyms");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<List<GymChoiceInfoResponse>>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body, Has.Exactly(1).Items);
        Assert.That(body![0].Name, Is.EqualTo("Active Gym"));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/gym/{id}/getGym", "own", "owner-allow")]
    public async Task GetGym_WithValidId_ReturnsGym()
    {
        var user = await SeedUserAsync(name: "gymuser", email: "gym@example.com");
        var gym = await SeedGymAsync(user.Id, "My Gym");
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync($"/api/gym/{gym.Id}/getGym");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<GymFormResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Id, Is.EqualTo(gym.Id.ToString()));
        Assert.That(body.Name, Is.EqualTo("My Gym"));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/gym/editGym", "own", "owner-allow")]
    public async Task EditGym_WithValidData_UpdatesGym()
    {
        var user = await SeedUserAsync(name: "gymuser", email: "gym@example.com");
        var gym = await SeedGymAsync(user.Id, "Old Name");
        SetAuthorizationHeader(user.Id);

        var request = new
        {
            _id = gym.Id.ToString(),
            name = "New Name"
        };

        var response = await PostAsJsonWithApiOptionsAsync("/api/gym/editGym", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var updatedGym = await db.Gyms.FirstOrDefaultAsync(g => g.Id == gym.Id);
        Assert.That(updatedGym, Is.Not.Null);
        Assert.That(updatedGym!.Name, Is.EqualTo("New Name"));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/gym/{id}/getGym", "own", "foreign-object-denial-no-mutation")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/gym/{id}/getGyms", "own", "foreign-object-denial-no-mutation")]
    public async Task GymReadRoutes_WithForeignResources_ReturnForbidden()
    {
        var owner = await SeedUserAsync(name: "gym-read-owner", email: "gym-read-owner@example.com");
        var attacker = await SeedUserAsync(name: "gym-read-attacker", email: "gym-read-attacker@example.com");
        var gym = await SeedGymAsync(owner.Id, "Private Gym");
        SetAuthorizationHeader(attacker.Id);

        using var detailResponse = await Client.GetAsync($"/api/gym/{gym.Id}/getGym");
        using var listResponse = await Client.GetAsync($"/api/gym/{owner.Id}/getGyms");

        Assert.That(detailResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    private async Task<Gym> SeedGymAsync(Id<User> userId, string name, bool isDeleted = false)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var gym = new Gym
        {
            Id = Id<Gym>.New(),
            UserId = userId,
            Name = name,
            IsDeleted = isDeleted
        };

        db.Gyms.Add(gym);
        await db.SaveChangesAsync();

        return gym;
    }

    private sealed class MessageResponse
    {
        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class GymChoiceInfoResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("lastTrainingInfo")]
        public LastTrainingGymInfoResponse? LastTrainingInfo { get; set; }
    }

    private sealed class LastTrainingGymInfoResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("type")]
        public LastTrainingGymPlanDayResponse? Type { get; set; }
    }

    private sealed class LastTrainingGymPlanDayResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class GymFormResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("address")]
        public string? Address { get; set; }
    }
}
