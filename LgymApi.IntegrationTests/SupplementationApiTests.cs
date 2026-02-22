using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class SupplementationApiTests : IntegrationTestBase
{
    [Test]
    public async Task SupplementScheduleAndAdherenceFlow_Works()
    {
        var trainer = await SeedTrainerAsync("trainer-supp", "trainer-supp@example.com");
        var trainee = await SeedUserAsync(name: "trainee-supp", email: "trainee-supp@example.com", password: "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var dayMask = MaskForDate(date);

        SetAuthorizationHeader(trainer.Id);
        var createPlanResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans", new
        {
            name = "Cut Stack",
            notes = "Week 1",
            items = new object[]
            {
                new { supplementName = "Omega 3", dosage = "2 caps", timeOfDay = "08:00", daysOfWeekMask = dayMask, order = 0 }
            }
        });

        createPlanResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdPlan = await createPlanResponse.Content.ReadFromJsonAsync<SupplementPlanResponse>();
        createdPlan.Should().NotBeNull();

        var assignResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{createdPlan!.Id}/assign", content: null);
        assignResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        SetAuthorizationHeader(trainee.Id);
        var scheduleResponse = await Client.GetAsync($"/api/trainee/supplements/schedule?date={date:yyyy-MM-dd}");
        scheduleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var schedule = await scheduleResponse.Content.ReadFromJsonAsync<List<SupplementScheduleEntryResponse>>();
        schedule.Should().NotBeNull();
        schedule!.Should().ContainSingle();
        schedule[0].Taken.Should().BeFalse();

        var checkOffResponse = await Client.PostAsJsonAsync("/api/trainee/supplements/intakes/check-off", new
        {
            planItemId = schedule[0].PlanItemId,
            intakeDate = date,
            takenAt = DateTimeOffset.UtcNow
        });

        checkOffResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var scheduleAfterResponse = await Client.GetAsync($"/api/trainee/supplements/schedule?date={date:yyyy-MM-dd}");
        scheduleAfterResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var scheduleAfter = await scheduleAfterResponse.Content.ReadFromJsonAsync<List<SupplementScheduleEntryResponse>>();
        scheduleAfter.Should().NotBeNull();
        scheduleAfter![0].Taken.Should().BeTrue();

        SetAuthorizationHeader(trainer.Id);
        var complianceResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/supplements/compliance?fromDate={date:yyyy-MM-dd}&toDate={date:yyyy-MM-dd}");
        complianceResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var compliance = await complianceResponse.Content.ReadFromJsonAsync<SupplementComplianceResponse>();
        compliance.Should().NotBeNull();
        compliance!.PlannedDoses.Should().Be(1);
        compliance.TakenDoses.Should().Be(1);
        compliance.AdherenceRate.Should().Be(100);
    }

    [Test]
    public async Task SupplementationEndpoints_EnforceAuthorization()
    {
        var trainerOwner = await SeedTrainerAsync("trainer-owner", "trainer-owner@example.com");
        var trainerOther = await SeedTrainerAsync("trainer-other", "trainer-other@example.com");
        var trainee = await SeedUserAsync(name: "trainee-auth", email: "trainee-auth@example.com", password: "password123");
        await LinkTrainerAndTraineeAsync(trainerOwner.Id, trainee.Id);

        SetAuthorizationHeader(trainerOther.Id);
        var summaryByOtherTrainer = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/supplements/compliance?fromDate=2026-01-01&toDate=2026-01-02");
        summaryByOtherTrainer.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var regularUser = await SeedUserAsync(name: "normal-user", email: "normal-user@example.com", password: "password123");
        SetAuthorizationHeader(regularUser.Id);
        var trainerOnlyEndpoint = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans");
        trainerOnlyEndpoint.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task SupplementationEndpoints_InvalidIds_ReturnBadRequest()
    {
        var trainer = await SeedTrainerAsync("trainer-invalid-supp", "trainer-invalid-supp@example.com");
        SetAuthorizationHeader(trainer.Id);

        var badTraineeIdResponse = await Client.GetAsync("/api/trainer/trainees/not-a-guid/supplement-plans");
        var badPlanIdResponse = await Client.PostAsJsonAsync("/api/trainer/trainees/00000000-0000-0000-0000-000000000001/supplement-plans/not-a-guid/update", new
        {
            name = "x",
            items = new object[] { new { supplementName = "A", dosage = "1", timeOfDay = "08:00", daysOfWeekMask = 127, order = 0 } }
        });

        badTraineeIdResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        badPlanIdResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task TrainerSupplementPlanCrudEndpoints_Work()
    {
        var trainer = await SeedTrainerAsync("trainer-supp-crud", "trainer-supp-crud@example.com");
        var trainee = await SeedUserAsync(name: "trainee-supp-crud", email: "trainee-supp-crud@example.com", password: "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        var createResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans", new
        {
            name = "Bulk",
            notes = "v1",
            items = new object[]
            {
                new { supplementName = "Magnesium", dosage = "1 tab", timeOfDay = "21:00", daysOfWeekMask = 127, order = 0 }
            }
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<SupplementPlanResponse>();

        var listResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var plans = await listResponse.Content.ReadFromJsonAsync<List<SupplementPlanResponse>>();
        plans.Should().NotBeNull();
        plans!.Any(x => x.Id == created!.Id).Should().BeTrue();

        var updateResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{created!.Id}/update", new
        {
            name = "Bulk v2",
            notes = "v2",
            items = new object[]
            {
                new { supplementName = "Magnesium", dosage = "2 tabs", timeOfDay = "22:00", daysOfWeekMask = 127, order = 0 }
            }
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await updateResponse.Content.ReadFromJsonAsync<SupplementPlanResponse>();
        updated.Should().NotBeNull();
        updated!.Id.Should().NotBe(created.Id);

        var unassignResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/unassign", content: null);
        unassignResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{updated.Id}/delete", content: null);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task TraineeCheckOff_WithInvalidPlanItemId_ReturnsBadRequest()
    {
        var trainee = await SeedUserAsync(name: "trainee-invalid-check", email: "trainee-invalid-check@example.com", password: "password123");
        SetAuthorizationHeader(trainee.Id);

        var response = await Client.PostAsJsonAsync("/api/trainee/supplements/intakes/check-off", new
        {
            planItemId = "not-a-guid",
            intakeDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Compliance_WithTooLargeDateRange_ReturnsBadRequest()
    {
        var trainer = await SeedTrainerAsync("trainer-supp-range", "trainer-supp-range@example.com");
        var trainee = await SeedUserAsync(name: "trainee-supp-range", email: "trainee-supp-range@example.com", password: "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        var response = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/supplements/compliance?fromDate=2025-01-01&toDate=2026-12-31");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreatePlan_WithNullItems_ReturnsBadRequest()
    {
        var trainer = await SeedTrainerAsync("trainer-supp-null-items", "trainer-supp-null-items@example.com");
        var trainee = await SeedUserAsync(name: "trainee-supp-null-items", email: "trainee-supp-null-items@example.com", password: "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        var response = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans", new
        {
            name = "Plan",
            notes = "n",
            items = (object?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

    private async Task LinkTrainerAndTraineeAsync(Guid trainerId, Guid traineeId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.TrainerTraineeLinks.Add(new TrainerTraineeLink
        {
            Id = Guid.NewGuid(),
            TrainerId = trainerId,
            TraineeId = traineeId
        });
        await db.SaveChangesAsync();
    }

    private static int MaskForDate(DateOnly date)
    {
        var normalizedDay = ((int)date.DayOfWeek + 6) % 7;
        return 1 << normalizedDay;
    }

    private sealed class SupplementPlanResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }
    }

    private sealed class SupplementScheduleEntryResponse
    {
        [JsonPropertyName("planItemId")]
        public string PlanItemId { get; set; } = string.Empty;

        [JsonPropertyName("taken")]
        public bool Taken { get; set; }
    }

    private sealed class SupplementComplianceResponse
    {
        [JsonPropertyName("plannedDoses")]
        public int PlannedDoses { get; set; }

        [JsonPropertyName("takenDoses")]
        public int TakenDoses { get; set; }

        [JsonPropertyName("adherenceRate")]
        public double AdherenceRate { get; set; }
    }
}
