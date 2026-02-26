using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LgymApi.Application.Notifications;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class TrainingTests : IntegrationTestBase
{
    [SetUp]
    public void ResetEmailCapture()
    {
        Factory.EmailSender.Reset();
    }

    [Test]
    public async Task AddTraining_WhenSubscribed_SchedulesTrainingCompletedEmailWithNames()
    {
        const string userEmail = "training-email-subscribed@example.com";

        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "training-email-subscribed",
            email: userEmail,
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Bench Press", BodyParts.Chest);
        var gymId = await CreateGymViaEndpointAsync(userId, "Email Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "Email Plan");
        var planDayName = "Email Day";
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, planDayName, new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 2, Reps = "8" }
        });

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.EmailNotificationSubscriptions.AddAsync(new EmailNotificationSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                NotificationType = EmailNotificationTypes.TrainingCompleted
            });
            await db.SaveChangesAsync();
        }

        var request = new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = DateTime.UtcNow,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 8, weight = 100.0, unit = WeightUnits.Kilograms.ToString() },
                new { exercise = exerciseId.ToString(), series = 2, reps = 6, weight = 105.0, unit = WeightUnits.Kilograms.ToString() }
            }
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var verifyScope = Factory.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var log = await db.EmailNotificationLogs
                .Where(x => x.Type == EmailNotificationTypes.TrainingCompleted && x.RecipientEmail == userEmail)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            log.Should().NotBeNull();
            log!.Status.Should().Be(EmailNotificationStatus.Pending);

            using var payload = JsonDocument.Parse(log.PayloadJson);
            var root = payload.RootElement;
            var planDayProperty = root.TryGetProperty("planDayName", out var camelPlanDay)
                ? camelPlanDay
                : root.GetProperty("PlanDayName");
            planDayProperty.GetString().Should().Be(planDayName);

            var exercisesProperty = root.TryGetProperty("exercises", out var camelExercises)
                ? camelExercises
                : root.GetProperty("Exercises");
            var exercises = exercisesProperty.EnumerateArray().ToArray();
            exercises.Length.Should().Be(2);
            var firstExerciseName = exercises[0].TryGetProperty("exerciseName", out var camelExerciseName)
                ? camelExerciseName
                : exercises[0].GetProperty("ExerciseName");
            firstExerciseName.GetString().Should().Be("Bench Press");

            var firstSeries = exercises[0].TryGetProperty("series", out var camelSeries1)
                ? camelSeries1
                : exercises[0].GetProperty("Series");
            var secondSeries = exercises[1].TryGetProperty("series", out var camelSeries2)
                ? camelSeries2
                : exercises[1].GetProperty("Series");
            firstSeries.GetInt32().Should().Be(1);
            secondSeries.GetInt32().Should().Be(2);
        }
    }

    [Test]
    public async Task AddTraining_WhenNotSubscribed_DoesNotScheduleTrainingCompletedEmail()
    {
        const string userEmail = "training-email-unsubscribed@example.com";

        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "training-email-unsubscribed",
            email: userEmail,
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Squat", BodyParts.Quads);
        var gymId = await CreateGymViaEndpointAsync(userId, "No Email Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "No Email Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "No Email Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 1, Reps = "5" }
        });

        var request = new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = DateTime.UtcNow,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 5, weight = 120.0, unit = WeightUnits.Kilograms.ToString() }
            }
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logs = await db.EmailNotificationLogs
            .Where(x => x.Type == EmailNotificationTypes.TrainingCompleted && x.RecipientEmail == userEmail)
            .ToListAsync();

        logs.Should().BeEmpty();
    }

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
    public async Task AddTraining_WithMismatchedRouteUserId_ReturnsForbidden()
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

        var (otherUserId, _) = await RegisterUserViaEndpointAsync(
            name: "trainingother",
            email: "trainingother@example.com",
            password: "password123");

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

        var response = await PostAsJsonWithApiOptionsAsync($"/api/{otherUserId}/addTraining", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
        body!.TypePlanDayId.Should().Be(planDayId.ToString());
        body!.PlanDay.Should().NotBeNull();
        body.PlanDay!.Id.Should().Be(planDayId.ToString());
        body.PlanDay!.Name.Should().Be("Arms Day");
    }

    [Test]
    public async Task GetLastTraining_WithMismatchedRouteUserId_ReturnsForbidden()
    {
        var (_, userAToken) = await RegisterUserViaEndpointAsync(
            name: "lastmismatch-a",
            email: "lastmismatch-a@example.com",
            password: "password123");

        var (userBId, _) = await RegisterUserViaEndpointAsync(
            name: "lastmismatch-b",
            email: "lastmismatch-b@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userAToken);

        var response = await Client.GetAsync($"/api/{userBId}/getLastTraining");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
    public async Task GetTrainingByDate_WithMismatchedRouteUserId_ReturnsForbidden()
    {
        var (_, userAToken) = await RegisterUserViaEndpointAsync(
            name: "bydatemismatch-a",
            email: "bydatemismatch-a@example.com",
            password: "password123");

        var (userBId, _) = await RegisterUserViaEndpointAsync(
            name: "bydatemismatch-b",
            email: "bydatemismatch-b@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userAToken);

        var request = new { createdAt = DateTime.UtcNow };
        var response = await PostAsJsonWithApiOptionsAsync($"/api/{userBId}/getTrainingByDate", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
    public async Task GetTrainingDates_WithMismatchedRouteUserId_ReturnsForbidden()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "datesuser3",
            email: "dates3@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var (otherUserId, _) = await RegisterUserViaEndpointAsync(
            name: "datesother",
            email: "datesother@example.com",
            password: "password123");

        var response = await Client.GetAsync($"/api/{otherUserId}/getTrainingDates");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
    public async Task AddTraining_CreatesMainRecordFromBestWeightInPayload()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "maxcreateuser",
            email: "maxcreate@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Max Bench", BodyParts.Chest);
        var gymId = await CreateGymViaEndpointAsync(userId, "Max Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "Max Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Max Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 3, Reps = "5" }
        });

        var trainingDate = DateTime.UtcNow;
        var request = new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = trainingDate,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 5, weight = 80.0, unit = WeightUnits.Kilograms.ToString() },
                new { exercise = exerciseId.ToString(), series = 2, reps = 5, weight = 85.0, unit = WeightUnits.Kilograms.ToString() }
            }
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var record = await db.MainRecords.SingleAsync(r => r.UserId == userId && r.ExerciseId == exerciseId);

        record.Weight.Should().Be(85.0);
        record.Unit.Should().Be(WeightUnits.Kilograms);
        record.Date.UtcDateTime.Should().BeCloseTo(trainingDate, TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task AddTraining_WithBetterResultInDifferentUnit_UpdatesMainRecordAndKeepsSourceUnit()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "maxupdateuser",
            email: "maxupdate@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Max Deadlift", BodyParts.Back);
        var gymId = await CreateGymViaEndpointAsync(userId, "Max Update Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "Max Update Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Max Update Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 3, Reps = "5" }
        });

        var oldDate = DateTime.UtcNow.AddDays(-7);
        await PostAsJsonWithApiOptionsAsync($"/api/mainRecords/{userId}/addNewRecord", new
        {
            exercise = exerciseId.ToString(),
            weight = 100.0,
            unit = WeightUnits.Kilograms.ToString(),
            date = oldDate
        });

        var trainingDate = DateTime.UtcNow;
        var response = await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = trainingDate,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 3, weight = 225.0, unit = WeightUnits.Pounds.ToString() }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var records = await db.MainRecords.Where(r => r.UserId == userId && r.ExerciseId == exerciseId).ToListAsync();

        records.Should().HaveCount(2);
        records.Should().Contain(r => r.Weight == 100.0 && r.Unit == WeightUnits.Kilograms);
        records.Should().Contain(r => r.Weight == 225.0 && r.Unit == WeightUnits.Pounds);
        records.Should().Contain(r => r.Weight == 225.0 && r.Unit == WeightUnits.Pounds &&
                                     r.Date.UtcDateTime >= trainingDate.AddSeconds(-1));
    }

    [Test]
    public async Task AddTraining_WithEqualResultAcrossUnits_DoesNotChangeMainRecords()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "maxequaluser",
            email: "maxequal@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Max Press", BodyParts.Shoulders);
        var gymId = await CreateGymViaEndpointAsync(userId, "Max Equal Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "Max Equal Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Max Equal Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 3, Reps = "5" }
        });

        var initialDate = DateTime.UtcNow.AddDays(-5);
        await PostAsJsonWithApiOptionsAsync($"/api/mainRecords/{userId}/addNewRecord", new
        {
            exercise = exerciseId.ToString(),
            weight = 100.0,
            unit = WeightUnits.Kilograms.ToString(),
            date = initialDate
        });

        var equivalentPounds = 100.0 / 0.45359237;
        var trainingDate = DateTime.UtcNow;
        var response = await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = trainingDate,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 5, weight = equivalentPounds, unit = WeightUnits.Pounds.ToString() }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var records = await db.MainRecords.Where(r => r.UserId == userId && r.ExerciseId == exerciseId).ToListAsync();

        records.Should().HaveCount(1);
        records[0].Weight.Should().Be(100.0);
        records[0].Unit.Should().Be(WeightUnits.Kilograms);
        records[0].Date.UtcDateTime.Should().BeCloseTo(initialDate, TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task AddTraining_WithWorseResultAcrossUnits_DoesNotUpdateMainRecord()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "maxworseuser",
            email: "maxworse@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Max Row", BodyParts.Back);
        var gymId = await CreateGymViaEndpointAsync(userId, "Max Worse Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "Max Worse Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Max Worse Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 3, Reps = "5" }
        });

        var initialDate = DateTime.UtcNow.AddDays(-4);
        await PostAsJsonWithApiOptionsAsync($"/api/mainRecords/{userId}/addNewRecord", new
        {
            exercise = exerciseId.ToString(),
            weight = 100.0,
            unit = WeightUnits.Kilograms.ToString(),
            date = initialDate
        });

        var trainingDate = DateTime.UtcNow;
        var response = await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = trainingDate,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 5, weight = 200.0, unit = WeightUnits.Pounds.ToString() }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var record = await db.MainRecords.SingleAsync(r => r.UserId == userId && r.ExerciseId == exerciseId);

        record.Weight.Should().Be(100.0);
        record.Unit.Should().Be(WeightUnits.Kilograms);
        record.Date.UtcDateTime.Should().BeCloseTo(initialDate, TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task AddTraining_WithMultipleSeriesForExercise_AddsOnlyBestMainRecordFromPayload()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "maxseriesuser",
            email: "maxseries@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var exerciseId = await CreateExerciseViaEndpointAsync(userId, "Bench Press", BodyParts.Chest);
        var gymId = await CreateGymViaEndpointAsync(userId, "Series Gym");
        var planId = await CreatePlanViaEndpointAsync(userId, "Series Plan");
        var planDayId = await CreatePlanDayViaEndpointAsync(userId, planId, "Series Day", new List<PlanDayExerciseInput>
        {
            new() { ExerciseId = exerciseId.ToString(), Series = 3, Reps = "5" }
        });

        var initialDate = DateTime.UtcNow.AddDays(-1);
        var initialResponse = await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = initialDate,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 5, weight = 70.0, unit = WeightUnits.Kilograms.ToString() }
            }
        });
        initialResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var initialMaxResponse = await Client.GetAsync($"/api/mainRecords/{userId}/getLastMainRecords");
        initialMaxResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialMaxBody = await initialMaxResponse.Content.ReadFromJsonAsync<List<MainRecordBestResponse>>();
        initialMaxBody.Should().NotBeNull();
        initialMaxBody.Should().HaveCount(1);
        initialMaxBody![0].Weight.Should().Be(70.0);

        var trainingDate = DateTime.UtcNow;
        var response = await PostAsJsonWithApiOptionsAsync($"/api/{userId}/addTraining", new
        {
            gym = gymId.ToString(),
            type = planDayId.ToString(),
            createdAt = trainingDate,
            exercises = new[]
            {
                new { exercise = exerciseId.ToString(), series = 1, reps = 5, weight = 72.0, unit = WeightUnits.Kilograms.ToString() },
                new { exercise = exerciseId.ToString(), series = 2, reps = 5, weight = 74.0, unit = WeightUnits.Kilograms.ToString() },
                new { exercise = exerciseId.ToString(), series = 3, reps = 5, weight = 76.0, unit = WeightUnits.Kilograms.ToString() }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedMaxResponse = await Client.GetAsync($"/api/mainRecords/{userId}/getLastMainRecords");
        updatedMaxResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedMaxBody = await updatedMaxResponse.Content.ReadFromJsonAsync<List<MainRecordBestResponse>>();
        updatedMaxBody.Should().NotBeNull();
        updatedMaxBody.Should().HaveCount(1);
        updatedMaxBody![0].Weight.Should().Be(76.0);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var records = await db.MainRecords
            .Where(r => r.UserId == userId && r.ExerciseId == exerciseId)
            .OrderBy(r => r.Date)
            .ToListAsync();

        records.Should().HaveCount(2);
        records.Should().Contain(r => r.Weight == 70.0 && r.Unit == WeightUnits.Kilograms);
        records.Should().Contain(r => r.Weight == 76.0 && r.Unit == WeightUnits.Kilograms);
        records.Should().NotContain(r => r.Weight == 72.0 && r.Unit == WeightUnits.Kilograms);
        records.Should().NotContain(r => r.Weight == 74.0 && r.Unit == WeightUnits.Kilograms);
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

    private sealed class MainRecordBestResponse
    {
        [JsonPropertyName("weight")]
        public double Weight { get; set; }
    }
}
