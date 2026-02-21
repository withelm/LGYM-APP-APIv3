using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class ReportingEngineTests : IntegrationTestBase
{
    [Test]
    public async Task ReportFlow_TrainerCreatesRequest_TraineeSubmits_TrainerCanReadSubmission()
    {
        var trainer = await SeedTrainerAsync("trainer-reports", "trainer-reports@example.com");
        var trainee = await SeedUserAsync(name: "trainee-reports", email: "trainee-reports@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Guid.NewGuid(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id
            });
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        var createTemplateResponse = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "Weekly Check-in",
            description = "Basic wellness report",
            fields = new object[]
            {
                new { key = "weight", label = "Weight", type = "Number", isRequired = true, order = 0 },
                new { key = "sleptWell", label = "Slept Well", type = "Boolean", isRequired = false, order = 1 }
            }
        });

        createTemplateResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var template = await createTemplateResponse.Content.ReadFromJsonAsync<ReportTemplateResponse>();
        template.Should().NotBeNull();
        template!.Fields.Should().HaveCount(2);

        var createRequestResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new
        {
            templateId = template.Id,
            dueAt = DateTimeOffset.UtcNow.AddDays(2)
        });

        createRequestResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var request = await createRequestResponse.Content.ReadFromJsonAsync<ReportRequestResponse>();
        request.Should().NotBeNull();
        request!.Status.Should().Be("Pending");

        SetAuthorizationHeader(trainee.Id);
        var pendingResponse = await Client.GetAsync("/api/trainee/report-requests");
        pendingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var pending = await pendingResponse.Content.ReadFromJsonAsync<List<ReportRequestResponse>>();
        pending.Should().NotBeNull();
        pending!.Should().ContainSingle(x => x.Id == request.Id);

        var submitResponse = await Client.PostAsJsonAsync($"/api/trainee/report-requests/{request.Id}/submit", new
        {
            answers = new
            {
                weight = 81.2,
                sleptWell = true
            }
        });

        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthorizationHeader(trainer.Id);
        var submissionsResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/report-submissions");
        submissionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var submissions = await submissionsResponse.Content.ReadFromJsonAsync<List<ReportSubmissionResponse>>();
        submissions.Should().NotBeNull();
        submissions!.Should().ContainSingle();
        submissions[0].ReportRequestId.Should().Be(request.Id);
        submissions[0].Answers["weight"].GetDouble().Should().BeApproximately(81.2, 0.001);
    }

    [Test]
    public async Task SubmitReport_WithInvalidDynamicFieldType_ReturnsBadRequest()
    {
        var trainer = await SeedTrainerAsync("trainer-reports-invalid", "trainer-reports-invalid@example.com");
        var trainee = await SeedUserAsync(name: "trainee-reports-invalid", email: "trainee-reports-invalid@example.com", password: "password123");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Guid.NewGuid(),
                TrainerId = trainer.Id,
                TraineeId = trainee.Id
            });
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(trainer.Id);
        var templateResponse = await Client.PostAsJsonAsync("/api/trainer/report-templates", new
        {
            name = "Daily",
            fields = new object[]
            {
                new { key = "mood", label = "Mood", type = "Text", isRequired = true, order = 0 }
            }
        });
        var template = await templateResponse.Content.ReadFromJsonAsync<ReportTemplateResponse>();

        var requestResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/report-requests", new
        {
            templateId = template!.Id
        });
        var request = await requestResponse.Content.ReadFromJsonAsync<ReportRequestResponse>();

        SetAuthorizationHeader(trainee.Id);
        var submitResponse = await Client.PostAsJsonAsync($"/api/trainee/report-requests/{request!.Id}/submit", new
        {
            answers = new
            {
                mood = 123
            }
        });

        submitResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<User> SeedTrainerAsync(string name, string email)
    {
        var trainer = await SeedUserAsync(name: name, email: email, password: "password123");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alreadyLinked = await db.UserRoles.AnyAsync(ur => ur.UserId == trainer.Id && ur.RoleId == AppDbContext.TrainerRoleSeedId);
        if (!alreadyLinked)
        {
            db.UserRoles.Add(new UserRole
            {
                UserId = trainer.Id,
                RoleId = AppDbContext.TrainerRoleSeedId
            });
            await db.SaveChangesAsync();
        }

        return trainer;
    }

    private sealed class ReportTemplateResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("fields")]
        public List<ReportTemplateFieldResponse> Fields { get; set; } = [];
    }

    private sealed class ReportTemplateFieldResponse
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;
    }

    private sealed class ReportRequestResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }

    private sealed class ReportSubmissionResponse
    {
        [JsonPropertyName("reportRequestId")]
        public string ReportRequestId { get; set; } = string.Empty;

        [JsonPropertyName("answers")]
        public Dictionary<string, JsonElement> Answers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
