using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.Trainer.Controllers;
using LgymApi.Api.Middleware;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Reporting.ApiAdapters;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using LgymApi.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("PostgreSql")]
internal sealed class PostgreSqlApiCompatibilityTests : PostgreSqlIntegrationTestBase
{
    [Test]
    public async Task TrainerSignedPhotoRead_UsesTypedPhotoIdAndPreservesInvalidIdContracts()
    {
        var trainer = await SeedTrainerAsync("postgres-photo-trainer", "postgres-photo-trainer@example.com");
        var trainee = await SeedUserAsync("postgres-photo-trainee", "postgres-photo-trainee@example.com");
        await LinkTrainerToTraineeAsync(trainer, trainee);
        await AuthenticateAsAsync(trainer);

        var reportRequestId = await CreatePhotoReportRequestAsync(trainer, trainee);
        var photoId = await SeedPhotoAsync(reportRequestId, trainee);

        var validResponse = await Client.GetAsync($"/api/trainer/reporting/photos/{photoId}/signed-url");
        Assert.That(validResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using (var validJson = await ReadJsonAsync(validResponse))
        {
            Assert.That(ReadRequiredString(validJson.RootElement, "readUrl"), Does.StartWith("http"));
            Assert.That(string.IsNullOrWhiteSpace(ReadRequiredString(validJson.RootElement, "expiresAt")), Is.False);
        }

        var malformedResponse = await Client.GetAsync("/api/trainer/reporting/photos/not-a-guid/signed-url");
        Assert.That(malformedResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        using (var malformedJson = await ReadJsonAsync(malformedResponse))
        {
            AssertLegacyMessage(malformedJson.RootElement, "Invalid photo ID format");
        }

        var emptyResult = await InvokeEmptySignedPhotoActionAsync();
        Assert.That(emptyResult.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var assertedValue53 = emptyResult.Value;
        Assert.That(assertedValue53, Is.TypeOf<ResponseMessageDto>());
        var emptyPayload = ((ResponseMessageDto)assertedValue53);
        Assert.That(emptyPayload.Message, Is.EqualTo(Messages.FieldRequired));

        using var emptyJson = JsonDocument.Parse(JsonSerializer.Serialize(emptyPayload));
        AssertLegacyMessage(emptyJson.RootElement, Messages.FieldRequired);
    }

    [Test]
    public async Task ExerciseScoresChart_PreservesLegacyNamesAndSerializesExerciseIdAsUuidString()
    {
        var user = await SeedUserAsync("postgres-chart-user", "postgres-chart-user@example.com");
        await AuthenticateAsAsync(user);

        var exerciseId = await CreateExerciseAsync(user, "PostgreSQL Chart Exercise");
        var gymId = await CreateGymAsync(user, "PostgreSQL Chart Gym");
        var planId = await CreatePlanAsync(user, "PostgreSQL Chart Plan");
        var planDayId = await CreatePlanDayAsync(planId, exerciseId, "PostgreSQL Chart Day");

        var trainingResponse = await PostAsJsonWithIdempotencyAsync($"/api/{user.Id}/addTraining", new
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
                    weight = 50.0,
                    unit = WeightUnits.Kilograms.ToString()
                }
            }
        });
        Assert.That(trainingResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var chartResponse = await Client.PostAsJsonAsync(
            $"/api/exerciseScores/{user.Id}/getExerciseScoresChartData",
            new { exerciseId = exerciseId.ToString() });
        Assert.That(chartResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var chartJson = await ReadJsonAsync(chartResponse);
        Assert.That(chartJson.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
        var entry = chartJson.RootElement.EnumerateArray().First();
        Assert.That(string.IsNullOrWhiteSpace(ReadRequiredString(entry, "_id")), Is.False);
        Assert.That(entry.TryGetProperty("id", out _), Is.False, "the chart retains the legacy _id property name");
        AssertUuidString(entry, "exerciseId", exerciseId.ToString());
    }

    [Test]
    public async Task EloRegistryChart_PreservesLegacyIdAsUuidString()
    {
        var user = await SeedUserAsync("postgres-elo-user", "postgres-elo-user@example.com");
        await AuthenticateAsAsync(user);

        var response = await Client.GetAsync($"/api/eloRegistry/{user.Id}/getEloRegistryChart");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var json = await ReadJsonAsync(response);
        Assert.That(json.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
        var entry = json.RootElement.EnumerateArray().First();
        AssertUuidString(entry, "_id");
        Assert.That(entry.TryGetProperty("id", out _), Is.False, "the chart retains the legacy _id property name");
    }

    private async Task<User> SeedTrainerAsync(string name, string email)
    {
        var trainer = await SeedUserAsync(name, email);

        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        database.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(database, trainer.Id));
        await database.SaveChangesAsync();

        return trainer;
    }

    private async Task LinkTrainerToTraineeAsync(User trainer, User trainee)
    {
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        database.TrainerTraineeLinks.Add(new TrainerTraineeLink
        {
            Id = Id<TrainerTraineeLink>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id
        });
        await database.SaveChangesAsync();
    }

    private async Task AuthenticateAsAsync(User user)
    {
        var response = await Client.PostAsJsonAsync("/api/login", new
        {
            name = user.Name,
            password = "password123"
        });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var json = await ReadJsonAsync(response);
        var token = ReadRequiredString(json.RootElement, "token");
        Assert.That(json.RootElement.TryGetProperty("req", out var requestUser), Is.True, "login retains the legacy req property name");
        AssertUuidString(requestUser, "_id", user.Id.ToString());
        Assert.That(json.RootElement.TryGetProperty("user", out _), Is.False, "login must not rename req to user");

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<Id<ReportRequest>> CreatePhotoReportRequestAsync(User trainer, User trainee)
    {
        var templateResponse = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "PostgreSQL Signed Photo Report",
            fields = new object[]
            {
                new
                {
                    key = "photos",
                    label = "Progress Photos",
                    type = "Photos",
                    isRequired = true,
                    order = 0,
                    moduleConfig = new { requiredViews = new[] { "Front" } }
                }
            }
        });
        Assert.That(templateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var templateJson = await ReadJsonAsync(templateResponse);
        var templateId = ParseId<ReportTemplate>(ReadRequiredString(templateJson.RootElement, "_id"));

        var requestResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new
        {
            templateId = templateId.ToString()
        });
        Assert.That(requestResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var requestJson = await ReadJsonAsync(requestResponse);
        return ParseId<ReportRequest>(ReadRequiredString(requestJson.RootElement, "_id"));
    }

    private async Task<Id<Photo>> SeedPhotoAsync(Id<ReportRequest> reportRequestId, User trainee)
    {
        var photoId = Id<Photo>.New();
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        database.Photos.Add(new Photo
        {
            Id = photoId,
            ReportRequestId = reportRequestId,
            OwnerUserId = trainee.Id,
            UploaderUserId = trainee.Id,
            ViewType = PhotoViewType.Front.ToString(),
            StorageKey = "photos/postgresql-compatibility.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 1024,
            Checksum = "postgresql-compatibility"
        });
        await database.SaveChangesAsync();
        return photoId;
    }

    private async Task<Id<Exercise>> CreateExerciseAsync(User user, string name)
    {
        var response = await PostAsJsonWithIdempotencyAsync($"/api/exercise/{user.Id}/addUserExercise", new
        {
            name,
            bodyPart = BodyParts.Chest.ToString(),
            description = "PostgreSQL compatibility exercise"
        });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        return ParseId<Exercise>(await FindItemIdByNameAsync($"/api/exercise/{user.Id}/getAllUserExercises", name));
    }

    private async Task<Id<Gym>> CreateGymAsync(User user, string name)
    {
        var response = await Client.PostAsJsonAsync($"/api/gym/{user.Id}/addGym", new { name });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        return ParseId<Gym>(await FindItemIdByNameAsync($"/api/gym/{user.Id}/getGyms", name));
    }

    private async Task<Id<Plan>> CreatePlanAsync(User user, string name)
    {
        var response = await Client.PostAsJsonAsync($"/api/{user.Id}/createPlan", new { name });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        return ParseId<Plan>(await FindItemIdByNameAsync($"/api/{user.Id}/getPlansList", name));
    }

    private async Task<Id<PlanDay>> CreatePlanDayAsync(Id<Plan> planId, Id<Exercise> exerciseId, string name)
    {
        var response = await PostAsJsonWithIdempotencyAsync($"/api/planDay/{planId}/createPlanDay", new
        {
            name,
            exercises = new[] { new { exercise = exerciseId.ToString(), series = 3, reps = "10" } }
        });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        return ParseId<PlanDay>(await FindItemIdByNameAsync($"/api/planDay/{planId}/getPlanDays", name));
    }

    private async Task<BadRequestObjectResult> InvokeEmptySignedPhotoActionAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var controller = new TrainerReportingController(
            Substitute.For<ITrainerReportTemplateApiPort>(),
            Substitute.For<ITrainerReportRequestApiPort>(),
            Substitute.For<ITrainerReportPhotoApiPort>(),
            Substitute.For<IRecurringReportAssignmentApiPort>(),
            serviceProvider.GetRequiredService<IMapper>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.Features.Set<IAuthenticatedAccountContextFeature>(new TestAuthenticatedAccountContextFeature(
            new AuthenticatedAccountContext(
                Id<AccountReference>.New(),
                null,
                [],
                [],
                false,
                false)));

        var result = await controller.GetPhotoSignedReadUrl(string.Empty);
        var assertedValue54 = result;
        Assert.That(assertedValue54, Is.TypeOf<BadRequestObjectResult>());
        return ((BadRequestObjectResult)assertedValue54);
    }

    private async Task<string> FindItemIdByNameAsync(string route, string name)
    {
        var response = await Client.GetAsync(route);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var json = await ReadJsonAsync(response);
        Assert.That(json.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
        var item = json.RootElement.EnumerateArray().Single(element =>
            element.TryGetProperty("name", out var itemName)
            && string.Equals(itemName.GetString(), name, StringComparison.Ordinal));
        return ReadRequiredString(item, "_id");
    }

    private async Task<HttpResponseMessage> PostAsJsonWithIdempotencyAsync<T>(string route, T request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("Idempotency-Key", $"postgresql-compatibility-{Id<PostgreSqlApiCompatibilityTests>.New()}");
        return await Client.SendAsync(message);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static Id<T> ParseId<T>(string value)
    {
        Assert.That(Id<T>.TryParse(value, out var id), Is.True, $"{value} must be a UUID string");
        return id;
    }

    private static void AssertLegacyMessage(JsonElement element, string expectedMessage)
    {
        Assert.That(ReadRequiredString(element, "msg"), Is.EqualTo(expectedMessage));
        Assert.That(element.TryGetProperty("message", out _), Is.False, "the legacy message property is msg");
    }

    private static void AssertUuidString(JsonElement element, string propertyName, string? expectedValue = null)
    {
        var value = ReadRequiredString(element, propertyName);
        Assert.That(Id<UuidValidationEntity>.TryParse(value, out var parsed), Is.True, $"{propertyName} must remain a UUID string");
        Assert.That(parsed.ToString(), Is.EqualTo(value));

        if (expectedValue != null)
        {
            Assert.That(value, Is.EqualTo(expectedValue));
        }
    }

    private static string ReadRequiredString(JsonElement element, string propertyName)
    {
        Assert.That(element.TryGetProperty(propertyName, out var property), Is.True, $"response must contain {propertyName}");
        Assert.That(property.ValueKind, Is.EqualTo(JsonValueKind.String), $"{propertyName} must remain a JSON string");
        var value = property.GetString();
        Assert.That(string.IsNullOrWhiteSpace(value), Is.False, $"{propertyName} must not be empty");
        return value!;
    }

    private sealed record TestAuthenticatedAccountContextFeature(AuthenticatedAccountContext Context)
        : IAuthenticatedAccountContextFeature;

    private sealed class UuidValidationEntity;
}
