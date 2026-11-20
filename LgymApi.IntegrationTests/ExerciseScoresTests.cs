using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class ExerciseScoresTests : IntegrationTestBase
{
    [Test]
    public async Task GetExerciseScoresChartData_WithNoScores_ReturnsEmptyList()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "chartuser",
            email: "chart@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Chart Exercise", BodyParts.Chest);

        var request = new { exerciseId = exerciseId.ToString() };
        var response = await Client.PostAsJsonAsync($"/api/exerciseScores/{userId}/getExerciseScoresChartData", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<List<ChartDataEntry>>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body, Is.Empty);
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exerciseScores/{id}/getExerciseScoresChartData", "own", "owner-allow")]
    public async Task GetExerciseScoresChartData_WithScores_ReturnsChartData()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "chartuser2",
            email: "chart2@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Chart Exercise 2", BodyParts.Chest);
        var gymId = await CreateGymViaEndpointAsync(userId, "Chart Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "Chart Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Chart Day", new List<PlanDayExerciseInput>
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
                new { exercise = exerciseId.ToString(), series = 1, reps = 10, weight = 50.0, unit = WeightUnits.Kilograms.ToString() },
                new { exercise = exerciseId.ToString(), series = 2, reps = 8, weight = 55.0, unit = WeightUnits.Kilograms.ToString() }
            }
        };

        await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", trainingRequest);

        var chartRequest = new { exerciseId = exerciseId.ToString() };
        var response = await Client.PostAsJsonAsync($"/api/exerciseScores/{userId}/getExerciseScoresChartData", chartRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<List<ChartDataEntry>>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That((body!.Select(entry => entry.ExerciseId)).All(id => id == exerciseId.ToString()), Is.True);

        var repeatResponse = await Client.PostAsJsonAsync($"/api/exerciseScores/{userId}/getExerciseScoresChartData", chartRequest);
        Assert.That(repeatResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var repeatBody = await repeatResponse.Content.ReadFromJsonAsync<List<ChartDataEntry>>();
        Assert.That(repeatBody, Is.Not.Null);
        Assert.That(repeatBody!.Select(entry => entry.Id), Is.EqualTo(body.Select(entry => entry.Id)));
        Assert.That(repeatBody.Select(entry => entry.ExerciseId), Is.EqualTo(body.Select(entry => entry.ExerciseId)));
        TestContext.Progress.WriteLine(System.Text.Json.JsonSerializer.Serialize(body));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exerciseScores/{id}/getExerciseScoresChartData", "own", "foreign-object-denial-no-mutation")]
    public async Task GetExerciseScoresChartData_WithMismatchedRouteUserId_ReturnsForbidden()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "authuser",
            email: "auth@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Auth Exercise", BodyParts.Chest);

        var (otherUserId, _) = await RegisterUserViaEndpointAsync(
            name: "chartother",
            email: "chartother@example.com",
            password: "password123");

        var request = new { exerciseId = exerciseId.ToString() };
        var response = await Client.PostAsJsonAsync($"/api/exerciseScores/{otherUserId}/getExerciseScoresChartData", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task GetExerciseScoresChartData_WithMalformedExerciseId_ReturnsBadRequest()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "chart-invalid-exercise",
            email: "chart-invalid-exercise@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await Client.PostAsJsonAsync($"/api/exerciseScores/{userId}/getExerciseScoresChartData", new
        {
            exerciseId = "not-a-uuid"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetLastExerciseScores_WithNoScores_ReturnsNullScores()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "lastscoresuser",
            email: "lastscores@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "LastScores Exercise", BodyParts.Back);

        var request = new
        {
            exerciseId = exerciseId.ToString(),
            exerciseName = "LastScores Exercise",
            series = 3
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/exercise/{userId}/getLastExerciseScores", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<LastExerciseScoresResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.ExerciseId, Is.EqualTo(exerciseId.ToString()));
        Assert.That(body.SeriesScores, Has.Exactly(3).Items);
        Assert.Multiple(() => { foreach (var s in body.SeriesScores) { Assert.That(s.Score, Is.Null); } });
    }

    [Test]
    public async Task GetLastExerciseScores_WithSeriesAboveLimit_ReturnsBadRequest()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "lastscoresuser-limit",
            email: "lastscores-limit@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "LastScores Limit Exercise", BodyParts.Back);

        var request = new
        {
            exerciseId = exerciseId.ToString(),
            exerciseName = "LastScores Limit Exercise",
            series = 31
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/exercise/{userId}/getLastExerciseScores", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/{id}/getLastExerciseScores", "own", "owner-allow")]
    public async Task GetLastExerciseScores_WithExistingScores_ReturnsPreviousScores()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "lastscoresuser2",
            email: "lastscores2@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "LastScores Exercise 2", BodyParts.Back);
        var gymId = await CreateGymViaEndpointAsync(userId, "LastScores Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "LastScores Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "LastScores Day", new List<PlanDayExerciseInput>
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
                new { exercise = exerciseId.ToString(), series = 1, reps = 10, weight = 60.0, unit = WeightUnits.Kilograms.ToString() },
                new { exercise = exerciseId.ToString(), series = 2, reps = 8, weight = 65.0, unit = WeightUnits.Kilograms.ToString() }
            }
        };

        await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", trainingRequest);

        var request = new
        {
            exerciseId = exerciseId.ToString(),
            exerciseName = "LastScores Exercise 2",
            series = 3,
            gymId = gymId.ToString()
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/exercise/{userId}/getLastExerciseScores", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<LastExerciseScoresResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.SeriesScores[0].Score, Is.Not.Null);
        Assert.That(body.SeriesScores[0].Score!.Weight, Is.EqualTo(60.0));
        Assert.That(body.SeriesScores[0].Score!.GymName, Is.EqualTo("LastScores Gym"));
    }

    [Test]
    public async Task GetLastExerciseScores_WithForeignCustomExercise_ReturnsNotFound()
    {
        var requester = await SeedUserAsync(name: "last-scores-requester", email: "last-scores-requester@example.com");
        var foreignOwner = await SeedUserAsync(name: "last-scores-foreign", email: "last-scores-foreign@example.com");
        var foreignExercise = new Exercise
        {
            Id = Id<Exercise>.New(),
            UserId = foreignOwner.Id,
            Name = "Foreign Last Scores Exercise",
            BodyPart = BodyParts.Back
        };
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.Exercises.Add(foreignExercise);
            await database.SaveChangesAsync();
        }
        SetAuthorizationHeader(requester.Id);

        var response = await PostAsJsonWithApiOptionsAsync($"/api/exercise/{requester.Id}/getLastExerciseScores", new
        {
            exerciseId = foreignExercise.Id.ToString(),
            exerciseName = foreignExercise.Name,
            series = 1
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetLastExerciseScores_WithForeignGym_PreservesScoreAndRedactsGymName()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "foreign-gym-last-scores-user",
            email: "foreign-gym-last-scores@example.com",
            password: "password123");
        var foreignOwner = await SeedUserAsync(
            name: "foreign-gym-last-scores-owner",
            email: "foreign-gym-last-scores-owner@example.com");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Foreign Gym Last Scores Exercise", BodyParts.Back);
        var gymId = await CreateGymViaEndpointAsync(userId, "Foreign Gym Last Scores");
        var planId = await CreatePlanViaEndpointAsync(userId, "Foreign Gym Last Scores Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Foreign Gym Last Scores Day", [
            new PlanDayExerciseInput { ExerciseId = exerciseId.ToString(), Series = 1, Reps = "10" }
        ]);
        var trainingResponse = await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = DateTime.UtcNow,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 10, weight = 60.0, unit = WeightUnits.Kilograms.ToString() }
            }
        });
        Assert.That(trainingResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await ReassignGymOwnerAsync(gymId, foreignOwner.Id);

        var response = await PostAsJsonWithApiOptionsAsync($"/api/exercise/{userId}/getLastExerciseScores", new
        {
            exerciseId = exerciseId.ToString(),
            exerciseName = "Foreign Gym Last Scores Exercise",
            series = 1,
            gymId = gymId.ToString()
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<LastExerciseScoresResponse>();
        Assert.That(body!.SeriesScores, Has.Exactly(1).Items);
        Assert.That(body.SeriesScores[0].Score!.Weight, Is.EqualTo(60.0));
        Assert.That(body.SeriesScores[0].Score!.GymName, Is.Null);
    }

    [Test]
    public async Task GetExerciseScoresFromTrainingByExercise_WithNoScores_ReturnsEmptyList()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "historyuser",
            email: "history@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "History Exercise", BodyParts.Quads);

        var request = new { exerciseId = exerciseId.ToString() };
        var response = await Client.PostAsJsonAsync("/api/exercise/getExerciseScoresFromTrainingByExercise", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<List<ExerciseHistoryItem>>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body, Is.Empty);
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/getExerciseScoresFromTrainingByExercise", "own", "owner-allow")]
    public async Task GetExerciseScoresFromTrainingByExercise_WithScores_ReturnsHistory()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "historyuser2",
            email: "history2@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "History Exercise 2", BodyParts.Quads);
        var gymId = await CreateGymViaEndpointAsync(userId, "History Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "History Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "History Day", new List<PlanDayExerciseInput>
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
                new { exercise = exerciseId.ToString(), series = 1, reps = 10, weight = 100.0, unit = WeightUnits.Kilograms.ToString() }
            }
        };

        await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", trainingRequest);

        var request = new { exerciseId = exerciseId.ToString() };
        var response = await Client.PostAsJsonAsync("/api/exercise/getExerciseScoresFromTrainingByExercise", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<List<ExerciseHistoryItem>>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body, Has.Count.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task GetExerciseScoresFromTrainingByExercise_WithForeignGym_PreservesScoreAndRedactsGymName()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "foreign-gym-history-user",
            email: "foreign-gym-history@example.com",
            password: "password123");
        var foreignOwner = await SeedUserAsync(
            name: "foreign-gym-history-owner",
            email: "foreign-gym-history-owner@example.com");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Foreign Gym History Exercise", BodyParts.Quads);
        var gymId = await CreateGymViaEndpointAsync(userId, "Foreign Gym History");
        var planId = await CreatePlanViaEndpointAsync(userId, "Foreign Gym History Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Foreign Gym History Day", [
            new PlanDayExerciseInput { ExerciseId = exerciseId.ToString(), Series = 1, Reps = "10" }
        ]);
        var trainingResponse = await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = DateTime.UtcNow,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 10, weight = 100.0, unit = WeightUnits.Kilograms.ToString() }
            }
        });
        Assert.That(trainingResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await ReassignGymOwnerAsync(gymId, foreignOwner.Id);

        var response = await Client.PostAsJsonAsync("/api/exercise/getExerciseScoresFromTrainingByExercise", new
        {
            exerciseId = exerciseId.ToString()
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<List<ExerciseHistoryItem>>();
        Assert.That(body, Has.Exactly(1).Items);
        Assert.That(body![0].GymName, Is.Empty);
        Assert.That(body[0].SeriesScores, Has.Exactly(1).Items);
        Assert.That(body[0].SeriesScores[0].Score!.Weight, Is.EqualTo(100.0));
        Assert.That(body[0].SeriesScores[0].Score!.GymName, Is.Null);
    }

    [Test]
    public async Task GetExerciseScoresFromTrainingByExercise_WhenScoresSharePlanDay_ReturnsHistory()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "sharedhistoryuser",
            email: "sharedhistory@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Shared History Exercise", BodyParts.Quads);
        var firstGymId = await CreateGymViaEndpointAsync(userId, "First Shared History Gym");
        var secondGymId = await CreateGymViaEndpointAsync(userId, "Second Shared History Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "Shared History Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Shared History Day", new List<PlanDayExerciseInput>
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
                new { exercise = exerciseId.ToString(), series = 1, reps = 10, weight = 100.0, unit = WeightUnits.Kilograms.ToString() }
            }
        };
        var secondTraining = new
        {
            gym = secondGymId.ToString(),
            type = planDayId.ToString(),
            createdAt = DateTime.UtcNow,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 8, weight = 110.0, unit = WeightUnits.Kilograms.ToString() }
            }
        };
        await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", firstTraining);
        await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", secondTraining);

        var request = new { exerciseId = exerciseId.ToString() };
        var response = await Client.PostAsJsonAsync("/api/exercise/getExerciseScoresFromTrainingByExercise", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<List<ExerciseHistoryItem>>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body, Has.Exactly(2).Items);
        Assert.That(body!.Select(item => item.GymName), Is.EquivalentTo(new[] { "First Shared History Gym", "Second Shared History Gym" }));
    }

    [Test]
    public async Task GetExerciseScoresFromTrainingByExercise_AfterPlanDeletion_KeepsHistory()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "deletedplanhistoryuser",
            email: "deletedplanhistory@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Deleted Plan History Exercise", BodyParts.Quads);
        var gymId = await CreateGymViaEndpointAsync(userId, "Deleted Plan History Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "Deleted History Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Deleted History Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 1, Reps = "10" }
        });
        var trainingResponse = await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = DateTime.UtcNow,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 10, weight = 100.0, unit = WeightUnits.Kilograms.ToString() }
            }
        });
        Assert.That(trainingResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var deletePlanResponse = await Client.PostAsync($"/api/{planId}/deletePlan", null);
        Assert.That(deletePlanResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var request = new { exerciseId = exerciseId.ToString() };
        var response = await Client.PostAsJsonAsync("/api/exercise/getExerciseScoresFromTrainingByExercise", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<List<ExerciseHistoryItem>>();
        Assert.That(body, Has.Exactly(1).Items);
        Assert.That(body![0].GymName, Is.EqualTo("Deleted Plan History Gym"));
        Assert.That(body[0].TrainingName, Is.Empty);
        Assert.That(body[0].SeriesScores, Has.Exactly(1).Items);
        Assert.That(body[0].SeriesScores[0].Score, Is.Not.Null);
        Assert.That(body[0].SeriesScores[0].Score!.Weight, Is.EqualTo(100.0));
    }

    [Test]
    public async Task ExerciseScoresChartRoute_PreservesLegacyJsonContract()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "chart-contract-user",
            email: "chart-contract@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Chart Contract Exercise", BodyParts.Chest);
        var gymId = await CreateGymViaEndpointAsync(userId, "Chart Contract Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "Chart Contract Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Chart Contract Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 1, Reps = "10" }
        });

        var createTrainingResponse = await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = DateTime.UtcNow,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 10, weight = 50.0, unit = WeightUnits.Kilograms.ToString() }
            }
        });
        Assert.That(createTrainingResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var chartResponse = await Client.PostAsJsonAsync($"/api/exerciseScores/{userId}/getExerciseScoresChartData", new
        {
            exerciseId = exerciseId.ToString()
        });

        Assert.That(chartResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var chartDocument = JsonDocument.Parse(await chartResponse.Content.ReadAsStringAsync());
        Assert.That(chartDocument.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(chartDocument.RootElement.GetArrayLength(), Is.GreaterThan(0));

        var chartEntry = chartDocument.RootElement[0];
        Assert.That(chartEntry.EnumerateObject().Select(property => property.Name), Is.EquivalentTo(new[] { "_id", "value", "date", "exerciseName", "exerciseId" }));
        Assert.That(chartEntry.GetProperty("_id").GetString(), Does.StartWith($"{exerciseId}-"));
        Assert.That(chartEntry.GetProperty("value").ValueKind, Is.EqualTo(JsonValueKind.Number));
        Assert.That(chartEntry.GetProperty("date").ValueKind, Is.EqualTo(JsonValueKind.String));
        Assert.That(chartEntry.GetProperty("exerciseName").GetString(), Is.EqualTo("Chart Contract Exercise"));
        Assert.That(chartEntry.GetProperty("exerciseId").GetString(), Is.EqualTo(exerciseId.ToString()));
    }

    private sealed class ChartDataEntry
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("exerciseId")]
        public string ExerciseId { get; set; } = string.Empty;
    }

    private sealed class LastExerciseScoresResponse
    {
        [JsonPropertyName("exerciseId")]
        public string ExerciseId { get; set; } = string.Empty;

        [JsonPropertyName("exerciseName")]
        public string ExerciseName { get; set; } = string.Empty;

        [JsonPropertyName("seriesScores")]
        public List<SeriesScoreItem> SeriesScores { get; set; } = new();
    }

    private sealed class SeriesScoreItem
    {
        [JsonPropertyName("series")]
        public int Series { get; set; }

        [JsonPropertyName("score")]
        public ScoreItem? Score { get; set; }
    }

    private sealed class ScoreItem
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("reps")]
        public int Reps { get; set; }

        [JsonPropertyName("weight")]
        public double Weight { get; set; }

        [JsonPropertyName("gymName")]
        public string? GymName { get; set; }
    }

    private sealed class ExerciseHistoryItem
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        [JsonPropertyName("gymName")]
        public string GymName { get; set; } = string.Empty;

        [JsonPropertyName("trainingName")]
        public string TrainingName { get; set; } = string.Empty;

        [JsonPropertyName("seriesScores")]
        public List<SeriesScoreItem> SeriesScores { get; set; } = new();
    }

    private async Task ReassignGymOwnerAsync(Id<Gym> gymId, Id<User> ownerId)
    {
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gym = await database.Gyms.FindAsync(gymId);
        gym!.UserId = ownerId;
        await database.SaveChangesAsync();
    }
}
