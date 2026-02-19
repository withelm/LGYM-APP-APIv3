using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class TrainingTests : IntegrationTestBase
{
    [Test]
    public async Task AddTraining_WithValidData_CreatesTrainingAndReturnsComparison()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "traininguser",
            email: "training@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Bench Press", BodyParts.Chest);
        var gymId = await CreateGymViaEndpointAsync(userId, "My Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "My Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Chest Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 3, Reps = "10" }
        });

        var request = new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = DateTime.UtcNow,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 10, weight = 60.0, unit = WeightUnits.Kilograms.ToString() },
                new { exercise = exerciseId.ToString(), series = 2, reps = 8, weight = 70.0, unit = WeightUnits.Kilograms.ToString() },
                new { exercise = exerciseId.ToString(), series = 3, reps = 6, weight = 80.0, unit = WeightUnits.Kilograms.ToString() }
            }
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TrainingSummaryResponse>();
        body.Should().NotBeNull();
        body!.Message.Should().Be("Created");
        body.Comparison.Should().NotBeNull();
        body.ProfileRank.Should().NotBeNull();
    }

    [Test]
    public async Task AddTraining_WithInvalidUserId_ReturnsNotFound()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "traininguser2",
            email: "training2@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Squat", BodyParts.Quads);
        var gymId = await CreateGymViaEndpointAsync(userId, "User Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "User Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Leg Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 3, Reps = "10" }
        });

        var nonExistentId = Guid.NewGuid();
        var request = new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = DateTime.UtcNow,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 10, weight = 100.0, unit = WeightUnits.Kilograms.ToString() }
            }
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/{nonExistentId}/addTraining", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task AddTraining_WithInvalidGymId_ReturnsNotFound()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "traininguser3",
            email: "training3@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Deadlift", BodyParts.Back);
        var planId = await CreatePlanViaEndpointAsync(userId, "User Plan 2");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Back Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 3, Reps = "5" }
        });

        var nonExistentGymId = Guid.NewGuid();
        var request = new
        {
            gym = nonExistentGymId.ToString(),
            type = planDayId.ToString(),
            createdAt = DateTime.UtcNow,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 5, weight = 150.0, unit = WeightUnits.Kilograms.ToString() }
            }
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task AddTraining_UpdatesEloAndRank()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "elouser",
            email: "elouser@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "OHP", BodyParts.Shoulders);
        var gymId = await CreateGymViaEndpointAsync(userId, "Elo Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "Elo Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Shoulders Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 3, Reps = "8" }
        });

        var request = new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = DateTime.UtcNow,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 10, weight = 40.0, unit = WeightUnits.Kilograms.ToString() }
            }
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TrainingSummaryResponse>();
        body.Should().NotBeNull();
        body!.UserOldElo.Should().Be(1000);
        body.ProfileRank.Should().NotBeNull();
        body.ProfileRank!.Name.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task GetLastTraining_WithNoTrainings_ReturnsNotFound()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "lastuser",
            email: "last@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync($"/api/{userId}/getLastTraining");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetLastTraining_WithTraining_ReturnsLastTraining()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "lastuser2",
            email: "last2@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Curls", BodyParts.Biceps);
        var gymId = await CreateGymViaEndpointAsync(userId, "Last Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "Last Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Arms Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 4, Reps = "12" }
        });

        var trainingRequest = new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = DateTime.UtcNow,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 12, weight = 15.0, unit = WeightUnits.Kilograms.ToString() }
            }
        };
        await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", trainingRequest);

        var response = await Client.GetAsync($"/api/{userId}/getLastTraining");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LastTrainingResponse>();
        body.Should().NotBeNull();
        body!.PlanDay.Should().NotBeNull();
        body.PlanDay!.Name.Should().Be("Arms Day");
    }

    [Test]
    public async Task GetTrainingByDate_WithNoTrainings_ReturnsNotFound()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "bydateuser",
            email: "bydate@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new { createdAt = DateTime.UtcNow };
        var response = await PostAsJsonWithApiOptionsAsync($"/api/{userId}/getTrainingByDate", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetTrainingByDate_WithTraining_ReturnsTrainingDetails()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "bydateuser2",
            email: "bydate2@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Rows", BodyParts.Back);
        var gymId = await CreateGymViaEndpointAsync(userId, "ByDate Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "ByDate Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Pull Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 4, Reps = "10" }
        });

        var trainingDate = DateTime.UtcNow;
        var trainingRequest = new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = trainingDate,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 10, weight = 60.0, unit = WeightUnits.Kilograms.ToString() },
                new { exercise = exerciseId.ToString(), series = 2, reps = 8, weight = 65.0, unit = WeightUnits.Kilograms.ToString() }
            }
        };
        await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", trainingRequest);

        var request = new { createdAt = trainingDate };
        var response = await PostAsJsonWithApiOptionsAsync($"/api/{userId}/getTrainingByDate", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<TrainingByDateResponse>>();
        body.Should().NotBeNull();
        body.Should().HaveCountGreaterThanOrEqualTo(1);
        body![0].PlanDay!.Name.Should().Be("Pull Day");
        body[0].Exercises.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task GetTrainingDates_WithNoTrainings_ReturnsNotFound()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "datesuser",
            email: "dates@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync($"/api/{userId}/getTrainingDates");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetTrainingDates_WithTrainings_ReturnsDates()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "datesuser2",
            email: "dates2@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Dips", BodyParts.Triceps);
        var gymId = await CreateGymViaEndpointAsync(userId, "Dates Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "Dates Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Push Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 3, Reps = "15" }
        });

        var trainingRequest = new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = DateTime.UtcNow,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 15, weight = 0.0, unit = WeightUnits.Kilograms.ToString() }
            }
        };
        await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", trainingRequest);

        var response = await Client.GetAsync($"/api/{userId}/getTrainingDates");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<DateTime>>();
        body.Should().NotBeNull();
        body.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task GetTrainingDates_WithInvalidUserId_ReturnsNotFound()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "datesuser3",
            email: "dates3@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var nonExistentId = Guid.NewGuid();
        var response = await Client.GetAsync($"/api/{nonExistentId}/getTrainingDates");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task AddTraining_WithMultipleExercises_TracksAllScores()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "multiuser",
            email: "multi@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId1 = await CreateExerciseViaEndpointAsync(userId, "Bench", BodyParts.Chest);
        var exerciseId2 = await CreateExerciseViaEndpointAsync(userId, "Flyes", BodyParts.Chest);
        var gymId = await CreateGymViaEndpointAsync(userId, "Multi Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "Multi Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Full Chest", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId1.ToString(), Series = 3, Reps = "10" },
            new() { ExerciseId = exerciseId2.ToString(), Series = 3, Reps = "12" }
        });

        var request = new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = DateTime.UtcNow,
            exercises = new[]
            {
                new { exercise = exerciseId1.ToString(), series = 1, reps = 10, weight = 60.0, unit = WeightUnits.Kilograms.ToString() },
                new { exercise = exerciseId1.ToString(), series = 2, reps = 8, weight = 70.0, unit = WeightUnits.Kilograms.ToString() },
                new { exercise = exerciseId2.ToString(), series = 1, reps = 12, weight = 15.0, unit = WeightUnits.Kilograms.ToString() }
            }
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TrainingSummaryResponse>();
        body.Should().NotBeNull();
        body!.Comparison.Should().HaveCount(2);
    }

    [Test]
    public async Task AddTraining_WithAliasUnit_ReturnsBadRequest()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "trainingstrict",
            email: "trainingstrict@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Strict Bench", BodyParts.Chest);
        var gymId = await CreateGymViaEndpointAsync(userId, "Strict Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "Strict Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Strict Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 3, Reps = "10" }
        });

        var request = new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = DateTime.UtcNow,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 10, weight = 60.0, unit = "kg" }
            }
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task AddTraining_WhenLatestEloEntryMissing_DoesNotPersistPartialData()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "rollbackuser",
            email: "rollback@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Rollback Bench", BodyParts.Chest);
        var gymId = await CreateGymViaEndpointAsync(userId, "Rollback Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "Rollback Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Rollback Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 3, Reps = "10" }
        });

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var eloEntries = await db.EloRegistries.Where(e => e.UserId == userId).ToListAsync();
            db.EloRegistries.RemoveRange(eloEntries);
            await db.SaveChangesAsync();
        }

        var request = new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = DateTime.UtcNow,
            exercises = new[]
            {
                new
                {
                    exercise = exerciseId.ToString(),
                    series = 1,
                    reps = 10,
                    weight = 60.0,
                    unit = WeightUnits.Kilograms.ToString()
                }
            }
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", request);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var trainings = await db.Trainings.Where(t => t.UserId == userId).ToListAsync();
            var scores = await db.ExerciseScores.Where(s => s.UserId == userId).ToListAsync();
            var links = await db.TrainingExerciseScores.ToListAsync();

            trainings.Should().BeEmpty();
            scores.Should().BeEmpty();
            links.Should().BeEmpty();
        }
    }

    private sealed class TrainingSummaryResponse
    {
        [JsonPropertyName("comparison")]
        public List<ComparisonItem> Comparison { get; set; } = new();

        [JsonPropertyName("gainElo")]
        public int GainElo { get; set; }

        [JsonPropertyName("userOldElo")]
        public int UserOldElo { get; set; }

        [JsonPropertyName("profileRank")]
        public RankResponse? ProfileRank { get; set; }

        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class ComparisonItem
    {
        [JsonPropertyName("exerciseId")]
        public string ExerciseId { get; set; } = string.Empty;

        [JsonPropertyName("exerciseName")]
        public string ExerciseName { get; set; } = string.Empty;
    }

    private sealed class RankResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("needElo")]
        public int NeedElo { get; set; }
    }

    private sealed class LastTrainingResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string TypePlanDayId { get; set; } = string.Empty;

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("planDay")]
        public PlanDayInfoResponse? PlanDay { get; set; }
    }

    private sealed class PlanDayInfoResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TrainingByDateResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("planDay")]
        public PlanDayInfoResponse? PlanDay { get; set; }

        [JsonPropertyName("gym")]
        public string? Gym { get; set; }

        [JsonPropertyName("exercises")]
        public List<ExerciseScoreResponse> Exercises { get; set; } = new();
    }

    private sealed class ExerciseScoreResponse
    {
        [JsonPropertyName("exerciseScoreId")]
        public string ExerciseScoreId { get; set; } = string.Empty;
    }
}
