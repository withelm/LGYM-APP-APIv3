using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class PlanDayTests : IntegrationTestBase
{
    [Test]
    public async Task CreatePlanDay_WithValidData_CreatesPlanDay()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "plandayuser",
            email: "planday@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "PlanDay Exercise", "Chest");
        var planId = await CreatePlanViaEndpointAsync(userId, "PlanDay Plan");

        var request = new
        {
            name = "Chest Day",
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 4, reps = "10" }
            }
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/planDay/{planId}/createPlanDay", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        body.Should().NotBeNull();
        body!.Message.Should().Be("Created");
    }

    [Test]
    public async Task CreatePlanDay_WithMissingName_ReturnsBadRequest()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "plandayuser2",
            email: "planday2@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "PlanDay Exercise 2", "Back");
        var planId = await CreatePlanViaEndpointAsync(userId, "PlanDay Plan 2");

        var request = new
        {
            name = "",
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 3, reps = "12" }
            }
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/planDay/{planId}/createPlanDay", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreatePlanDay_WithNoExercises_ReturnsBadRequest()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "plandayuser3",
            email: "planday3@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var planId = await CreatePlanViaEndpointAsync(userId, "PlanDay Plan 3");

        var request = new
        {
            name = "Empty Day",
            exercises = Array.Empty<object>()
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/planDay/{planId}/createPlanDay", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreatePlanDay_WithInvalidPlanId_ReturnsNotFound()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "plandayuser4",
            email: "planday4@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "PlanDay Exercise 4", "Shoulders");

        var nonExistentPlanId = Guid.NewGuid();
        var request = new
        {
            name = "Test Day",
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 3, reps = "10" }
            }
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/planDay/{nonExistentPlanId}/createPlanDay", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetPlanDay_WithValidId_ReturnsPlanDay()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "getdayuser",
            email: "getday@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "GetDay Exercise", "Quads");
        var planId = await CreatePlanViaEndpointAsync(userId, "GetDay Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Leg Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 5, Reps = "5" }
        });

        var response = await Client.GetAsync($"/api/planDay/{planDayId}/getPlanDay");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PlanDayVmResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("Leg Day");
        body.Exercises.Should().HaveCount(1);
        body.Exercises[0].Series.Should().Be(5);
        body.Exercises[0].Reps.Should().Be("5");
    }

    [Test]
    public async Task GetPlanDay_WithInvalidId_ReturnsNotFound()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "getdayuser2",
            email: "getday2@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var nonExistentId = Guid.NewGuid();
        var response = await Client.GetAsync($"/api/planDay/{nonExistentId}/getPlanDay");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetPlanDays_WithValidPlanId_ReturnsPlanDays()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "getdaysuser",
            email: "getdays@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "GetDays Exercise", "Back");
        var planId = await CreatePlanViaEndpointAsync(userId, "GetDays Plan");

        await CreatePlanDayViaEndpointAsync(userId, planId, "Day A", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 3, Reps = "10" }
        });

        await CreatePlanDayViaEndpointAsync(userId, planId, "Day B", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 4, Reps = "8" }
        });

        var response = await Client.GetAsync($"/api/planDay/{planId}/getPlanDays");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<PlanDayVmResponse>>();
        body.Should().NotBeNull();
        body.Should().HaveCount(2);
        body!.Select(pd => pd.Name).Should().Contain(new[] { "Day A", "Day B" });
    }

    [Test]
    public async Task GetPlanDays_WithNoPlanDays_ReturnsNotFound()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "emptydaysuser",
            email: "emptydays@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var planId = await CreatePlanViaEndpointAsync(userId, "Empty Plan");

        var response = await Client.GetAsync($"/api/planDay/{planId}/getPlanDays");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UpdatePlanDay_WithValidData_UpdatesPlanDay()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "updatedayuser",
            email: "updateday@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "UpdateDay Exercise", "Chest");
        var exerciseId2 = await CreateExerciseViaEndpointAsync(userId, "UpdateDay Exercise 2", "Triceps");
        var planId = await CreatePlanViaEndpointAsync(userId, "UpdateDay Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Original Name", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 3, Reps = "10" }
        });

        var updateRequest = new
        {
            _id = planDayId.ToString(),
            name = "Updated Name",
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 4, reps = "12" },
                new { exercise = exerciseId2.ToString(), series = 3, reps = "15" }
            }
        };

        var response = await PostAsJsonWithApiOptionsAsync("/api/planDay/updatePlanDay", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var verifyResponse = await Client.GetAsync($"/api/planDay/{planDayId}/getPlanDay");
        var updatedPlanDay = await verifyResponse.Content.ReadFromJsonAsync<PlanDayVmResponse>();
        updatedPlanDay!.Name.Should().Be("Updated Name");
        updatedPlanDay.Exercises.Should().HaveCount(2);
    }

    [Test]
    public async Task UpdatePlanDay_WithMissingName_ReturnsBadRequest()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "updatedayuser2",
            email: "updateday2@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "UpdateDay Exercise 3", "Back");
        var planId = await CreatePlanViaEndpointAsync(userId, "UpdateDay Plan 2");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Test Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 3, Reps = "10" }
        });

        var updateRequest = new
        {
            _id = planDayId.ToString(),
            name = "",
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 4, reps = "12" }
            }
        };

        var response = await PostAsJsonWithApiOptionsAsync("/api/planDay/updatePlanDay", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task DeletePlanDay_WithValidId_DeletesPlanDay()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "deletedayuser",
            email: "deleteday@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "DeleteDay Exercise", "Shoulders");
        var planId = await CreatePlanViaEndpointAsync(userId, "DeleteDay Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "To Delete", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 3, Reps = "10" }
        });

        var response = await Client.GetAsync($"/api/planDay/{planDayId}/deletePlanDay");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var verifyResponse = await Client.GetAsync($"/api/planDay/{planId}/getPlanDays");
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeletePlanDay_WithInvalidId_ReturnsNotFound()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "deletedayuser2",
            email: "deleteday2@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var nonExistentId = Guid.NewGuid();
        var response = await Client.GetAsync($"/api/planDay/{nonExistentId}/deletePlanDay");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetPlanDaysTypes_WithValidUserId_ReturnsTypes()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "typesuser",
            email: "types@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Types Exercise", "Biceps");
        var planId = await CreatePlanViaEndpointAsync(userId, "Types Plan");
        await CreatePlanDayViaEndpointAsync(userId, planId, "Arms Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 4, Reps = "12" }
        });

        var response = await Client.GetAsync($"/api/planDay/{userId}/getPlanDaysTypes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<PlanDayChooseResponse>>();
        body.Should().NotBeNull();
        body.Should().HaveCountGreaterThanOrEqualTo(1);
        body!.Should().Contain(pd => pd.Name == "Arms Day");
    }

    [Test]
    public async Task GetPlanDaysTypes_WithNoActivePlan_ReturnsNotFound()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "typesuser2",
            email: "types2@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync($"/api/planDay/{userId}/getPlanDaysTypes");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetPlanDaysInfo_WithValidPlanId_ReturnsInfo()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "infouser",
            email: "info@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Info Exercise", "Quads");
        var planId = await CreatePlanViaEndpointAsync(userId, "Info Plan");
        await CreatePlanDayViaEndpointAsync(userId, planId, "Info Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 3, Reps = "10" },
            new() { ExerciseId = exerciseId.ToString(), Series = 4, Reps = "8" }
        });

        var response = await Client.GetAsync($"/api/planDay/{planId}/getPlanDaysInfo");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<PlanDayInfoResponse>>();
        body.Should().NotBeNull();
        body.Should().HaveCount(1);
        body![0].Name.Should().Be("Info Day");
        body[0].TotalNumberOfExercises.Should().Be(2);
        body[0].TotalNumberOfSeries.Should().Be(7);
    }

    private sealed class MessageResponse
    {
        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class PlanDayVmResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("exercises")]
        public List<PlanDayExerciseVmResponse> Exercises { get; set; } = new();
    }

    private sealed class PlanDayExerciseVmResponse
    {
        [JsonPropertyName("series")]
        public int Series { get; set; }

        [JsonPropertyName("reps")]
        public string Reps { get; set; } = string.Empty;

        [JsonPropertyName("exercise")]
        public ExerciseResponse? Exercise { get; set; }
    }

    private sealed class ExerciseResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class PlanDayChooseResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class PlanDayInfoResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("lastTrainingDate")]
        public DateTime? LastTrainingDate { get; set; }

        [JsonPropertyName("totalNumberOfSeries")]
        public int TotalNumberOfSeries { get; set; }

        [JsonPropertyName("totalNumberOfExercises")]
        public int TotalNumberOfExercises { get; set; }
    }
}
