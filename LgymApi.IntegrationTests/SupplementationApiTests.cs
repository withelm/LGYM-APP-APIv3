using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using LgymApi.IntegrationTests.Authorization;
using LgymApi.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class SupplementationApiTests : IntegrationTestBase
{
    private CultureInfo? _originalCulture;
    private CultureInfo? _originalUiCulture;

    [SetUp]
    public void UseEnglishRequestCultureForGoldenAssertions()
    {
        _originalCulture = CultureInfo.CurrentCulture;
        _originalUiCulture = CultureInfo.CurrentUICulture;
        var englishCulture = CultureInfo.GetCultureInfo("en");
        CultureInfo.CurrentCulture = englishCulture;
        CultureInfo.CurrentUICulture = englishCulture;
        SetRequestCulture("en");
    }

    [TearDown]
    public void RestoreCultureAfterGoldenAssertions()
    {
        CultureInfo.CurrentCulture = _originalCulture!;
        CultureInfo.CurrentUICulture = _originalUiCulture!;
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainee/supplements/schedule", "own", "owner-allow")]
    [AuthorizationEvidence("GET", "/api/trainee/supplements/schedule", "own", "no-client-subject")]
    [AuthorizationEvidence("POST", "/api/trainee/supplements/intakes/check-off", "own", "owner-allow")]
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

        Assert.That(createPlanResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var createdPlan = await createPlanResponse.Content.ReadFromJsonAsync<SupplementPlanResponse>();
        Assert.That(createdPlan, Is.Not.Null);

        var assignResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{createdPlan!.Id}/assign", content: null);
        Assert.That(assignResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        SetAuthorizationHeader(trainee.Id);
        var scheduleResponse = await Client.GetAsync($"/api/trainee/supplements/schedule?date={date:yyyy-MM-dd}");
        Assert.That(scheduleResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var schedule = await scheduleResponse.Content.ReadFromJsonAsync<List<SupplementScheduleEntryResponse>>();
        Assert.That(schedule, Is.Not.Null);
        Assert.That(schedule!, Has.Exactly(1).Items);
        Assert.That(schedule[0].Taken, Is.False);

        var checkOffResponse = await Client.PostAsJsonAsync("/api/trainee/supplements/intakes/check-off", new
        {
            planItemId = schedule[0].PlanItemId,
            intakeDate = date,
            takenAt = DateTimeOffset.UtcNow
        });

        Assert.That(checkOffResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var scheduleAfterResponse = await Client.GetAsync($"/api/trainee/supplements/schedule?date={date:yyyy-MM-dd}");
        Assert.That(scheduleAfterResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var scheduleAfter = await scheduleAfterResponse.Content.ReadFromJsonAsync<List<SupplementScheduleEntryResponse>>();
        Assert.That(scheduleAfter, Is.Not.Null);
        Assert.That(scheduleAfter![0].Taken, Is.True);

        SetAuthorizationHeader(trainer.Id);
        var complianceResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/supplements/compliance?fromDate={date:yyyy-MM-dd}&toDate={date:yyyy-MM-dd}");
        Assert.That(complianceResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var compliance = await complianceResponse.Content.ReadFromJsonAsync<SupplementComplianceResponse>();
        Assert.That(compliance, Is.Not.Null);
        Assert.That(compliance!.PlannedDoses, Is.EqualTo(1));
        Assert.That(compliance.TakenDoses, Is.EqualTo(1));
        Assert.That(compliance.AdherenceRate, Is.EqualTo(100));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainee/supplements/schedule", "own", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainee/supplements/intakes/check-off", "own", "anonymous-denial")]
    public async Task TraineeSupplementRoutes_WithoutAuthentication_AreUnauthorized()
    {
        ClearAuthorizationHeader();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        using var scheduleResponse = await Client.GetAsync($"/api/trainee/supplements/schedule?date={date:yyyy-MM-dd}");
        using var checkOffResponse = await Client.PostAsJsonAsync("/api/trainee/supplements/intakes/check-off", new
        {
            planItemId = "00000000-0000-0000-0000-000000000001",
            intakeDate = date,
            takenAt = DateTimeOffset.UtcNow
        });

        Assert.That(scheduleResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(checkOffResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
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
        Assert.That(summaryByOtherTrainer.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        var regularUser = await SeedUserAsync(name: "normal-user", email: "normal-user@example.com", password: "password123");
        SetAuthorizationHeader(regularUser.Id);
        var trainerOnlyEndpoint = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans");
        Assert.That(trainerOnlyEndpoint.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
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

        Assert.That(badTraineeIdResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(badPlanIdResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
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
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await createResponse.Content.ReadFromJsonAsync<SupplementPlanResponse>();

        var listResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans");
        Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var plans = await listResponse.Content.ReadFromJsonAsync<List<SupplementPlanResponse>>();
        Assert.That(plans, Is.Not.Null);
        Assert.That(plans!.Any(x => x.Id == created!.Id), Is.True);

        var updateResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{created!.Id}/update", new
        {
            name = "Bulk v2",
            notes = "v2",
            items = new object[]
            {
                new { supplementName = "Magnesium", dosage = "2 tabs", timeOfDay = "22:00", daysOfWeekMask = 127, order = 0 }
            }
        });
        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var updated = await updateResponse.Content.ReadFromJsonAsync<SupplementPlanResponse>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Id, Is.Not.EqualTo(created.Id));

        var unassignResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/unassign", content: null);
        Assert.That(unassignResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var deleteResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{updated.Id}/delete", content: null);
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
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

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Compliance_WithTooLargeDateRange_ReturnsBadRequest()
    {
        var trainer = await SeedTrainerAsync("trainer-supp-range", "trainer-supp-range@example.com");
        var trainee = await SeedUserAsync(name: "trainee-supp-range", email: "trainee-supp-range@example.com", password: "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        var response = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/supplements/compliance?fromDate=2025-01-01&toDate=2026-12-31");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Compliance_WithoutRequiredQueryDates_ReturnsBadRequest()
    {
        var trainer = await SeedTrainerAsync("trainer-supp-missing-dates", "trainer-supp-missing-dates@example.com");
        var trainee = await SeedUserAsync(name: "trainee-supp-missing-dates", email: "trainee-supp-missing-dates@example.com", password: "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        var response = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/supplements/compliance");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
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

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task TraineeCheckOff_WithoutIntakeDate_ReturnsBadRequest()
    {
        var trainee = await SeedUserAsync(name: "trainee-missing-intake-date", email: "trainee-missing-intake-date@example.com", password: "password123");
        SetAuthorizationHeader(trainee.Id);

        var response = await Client.PostAsJsonAsync("/api/trainee/supplements/intakes/check-off", new
        {
            planItemId = Domain.ValueObjects.Id<object>.New().ToString()
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans/{planId}/update", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans/{planId}/delete", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans/{planId}/assign", "trainer-shared", "active-relationship-allow")]
    public async Task SupplementUpdate_ReplacesIdentityAndRetainsActiveState()
    {
        var trainer = await SeedTrainerAsync("trainer-supp-update", "trainer-supp-update@example.com");
        var trainee = await SeedUserAsync("trainee-supp-update", "trainee-supp-update@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        var created = await CreateSupplementPlanAsync(
            trainer.Id,
            trainee.Id,
            "Original stack",
            "Original notes",
            new SupplementPlanItemInput("Original item", "1 cap", "08:00", 127, 0));

        Assert.That(created.IsActive, Is.False);
        Assert.That(created.Items, Has.Exactly(1).Items);

        SetAuthorizationHeader(trainer.Id);
        var listResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans");
        Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var list = await ReadJsonAsync(listResponse))
        {
            Assert.That(list.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(list.RootElement.GetArrayLength(), Is.EqualTo(1));
            AssertPlanContract(list.RootElement[0]);
            Assert.That(list.RootElement[0].GetProperty("_id").GetString(), Is.EqualTo(created.Id));
            Assert.That(list.RootElement[0].GetProperty("name").GetString(), Is.EqualTo("Original stack"));
        }

        var assignResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{trainee.Id}/supplement-plans/{created.Id}/assign",
            content: null);
        await AssertMessageAsync(assignResponse, HttpStatusCode.OK, Messages.Updated);

        var updateResponse = await Client.PostAsJsonAsync(
            $"/api/trainer/trainees/{trainee.Id}/supplement-plans/{created.Id}/update",
            new
            {
                name = "Replacement stack",
                notes = "Replacement notes",
                items = new object[]
                {
                    new { supplementName = "Later", dosage = "2 caps", timeOfDay = "20:00", daysOfWeekMask = 127, order = 1 },
                    new { supplementName = "Earlier", dosage = "1 cap", timeOfDay = "08:00", daysOfWeekMask = 65, order = 0 }
                }
            });

        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        string updatedId;
        string[] updatedItemIds;
        using (var updated = await ReadJsonAsync(updateResponse))
        {
            AssertPlanContract(updated.RootElement);
            updatedId = updated.RootElement.GetProperty("_id").GetString()!;
            Assert.That(updatedId, Is.Not.EqualTo(created.Id));
            Assert.That(updated.RootElement.GetProperty("trainerId").GetString(), Is.EqualTo(trainer.Id.ToString()));
            Assert.That(updated.RootElement.GetProperty("traineeId").GetString(), Is.EqualTo(trainee.Id.ToString()));
            Assert.That(updated.RootElement.GetProperty("name").GetString(), Is.EqualTo("Replacement stack"));
            Assert.That(updated.RootElement.GetProperty("notes").GetString(), Is.EqualTo("Replacement notes"));
            Assert.That(updated.RootElement.GetProperty("isActive").GetBoolean(), Is.True);
            Assert.That(updated.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("supplementName").GetString()), Is.EqualTo(new[] { "Earlier", "Later" }));
            updatedItemIds = updated.RootElement.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("_id").GetString()!)
                .ToArray();
        }

        var persistedPlans = await GetSupplementPlansIgnoringFiltersAsync(trainee.Id);
        Assert.That(persistedPlans, Has.Exactly(2).Items);
        var replacedPlan = persistedPlans.Single(plan => plan.Id.ToString() == created.Id);
        var replacementPlan = persistedPlans.Single(plan => plan.Id.ToString() == updatedId);
        Assert.That(replacedPlan.IsDeleted, Is.True);
        Assert.That(replacedPlan.IsActive, Is.False);
        Assert.That(replacementPlan.IsDeleted, Is.False);
        Assert.That(replacementPlan.IsActive, Is.True);
        Assert.That(replacementPlan.Items.Select(item => item.Id.ToString()), Is.EqualTo(updatedItemIds));
        Assert.That(replacementPlan.Items.Select(item => item.Id.ToString())
            .Intersect(created.Items.Select(item => item.Id)), Is.Empty);

        var deleteResponse = await Client.PostAsync(
            $"/api/trainer/trainees/{trainee.Id}/supplement-plans/{updatedId}/delete",
            content: null);
        await AssertMessageAsync(deleteResponse, HttpStatusCode.OK, Messages.Deleted);

        persistedPlans = await GetSupplementPlansIgnoringFiltersAsync(trainee.Id);
        var deletedReplacement = persistedPlans.Single(plan => plan.Id.ToString() == updatedId);
        Assert.That(deletedReplacement.IsDeleted, Is.True);
        Assert.That(deletedReplacement.IsActive, Is.False);
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans/{planId}/update", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans/{planId}/update", "trainer-shared", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans/{planId}/delete", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans/{planId}/delete", "trainer-shared", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans/{planId}/assign", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans/{planId}/assign", "trainer-shared", "anonymous-denial")]
    public async Task SupplementUpdate_WithoutRelationshipOrAuthentication_DoesNotMutatePlans()
    {
        var owner = await SeedTrainerAsync("task10-unrelated-supp-update-owner", "task10-unrelated-supp-update-owner@example.com");
        var unrelatedTrainer = await SeedTrainerAsync("task10-unrelated-supp-update-trainer", "task10-unrelated-supp-update-trainer@example.com");
        var trainee = await SeedUserAsync("task10-unrelated-supp-update-trainee", "task10-unrelated-supp-update-trainee@example.com", "password123");
        await LinkTrainerAndTraineeAsync(owner.Id, trainee.Id);
        var plan = await CreateSupplementPlanAsync(owner.Id, trainee.Id, "Unrelated plan", "Original", new SupplementPlanItemInput("Item", "1", "08:00", 127, 0));
        var before = await GetSupplementPlanSnapshotsAsync(trainee.Id);

        SetAuthorizationHeader(unrelatedTrainer.Id);
        await AssertMessageAsync(
            await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{plan.Id}/update", ValidSupplementRequest()),
            HttpStatusCode.NotFound,
            Messages.DidntFind);
        Assert.That((await GetSupplementPlanSnapshotsAsync(trainee.Id)), Is.EqualTo(before));

        SetAuthorizationHeader(unrelatedTrainer.Id);
        await AssertMessageAsync(await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{plan.Id}/assign", null), HttpStatusCode.NotFound, Messages.DidntFind);
        Assert.That((await GetSupplementPlanSnapshotsAsync(trainee.Id)), Is.EqualTo(before));

        SetAuthorizationHeader(unrelatedTrainer.Id);
        await AssertMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{plan.Id}/delete", null),
            HttpStatusCode.NotFound,
            Messages.DidntFind);
        Assert.That((await GetSupplementPlanSnapshotsAsync(trainee.Id)), Is.EqualTo(before));

        ClearAuthorizationHeader();
        using (var assignResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{plan.Id}/assign", null))
        {
            Assert.That(assignResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        Assert.That((await GetSupplementPlanSnapshotsAsync(trainee.Id)), Is.EqualTo(before));

        using (var response = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{plan.Id}/update", ValidSupplementRequest()))
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        Assert.That((await GetSupplementPlanSnapshotsAsync(trainee.Id)), Is.EqualTo(before));

        using (var deleteResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{plan.Id}/delete", null))
        {
            Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        Assert.That((await GetSupplementPlanSnapshotsAsync(trainee.Id)), Is.EqualTo(before));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans/{planId}/update", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans/{planId}/delete", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans/{planId}/assign", "trainer-shared", "foreign-object-denial-no-mutation")]
    public async Task SupplementUpdate_ForAnotherTrainersPlan_ReturnsNotFoundAndPreservesPlans()
    {
        var owner = await SeedTrainerAsync("task10-foreign-supp-update-owner", "task10-foreign-supp-update-owner@example.com");
        var otherTrainer = await SeedTrainerAsync("task10-foreign-supp-update-trainer", "task10-foreign-supp-update-trainer@example.com");
        var trainee = await SeedUserAsync("task10-foreign-supp-update-trainee", "task10-foreign-supp-update-trainee@example.com", "password123");
        await LinkTrainerAndTraineeAsync(owner.Id, trainee.Id);
        await LinkTrainerAndTraineeAsync(otherTrainer.Id, trainee.Id);
        var plan = await CreateSupplementPlanAsync(owner.Id, trainee.Id, "Foreign plan", "Original", new SupplementPlanItemInput("Item", "1", "08:00", 127, 0));
        var before = await GetSupplementPlanSnapshotsAsync(trainee.Id);

        SetAuthorizationHeader(otherTrainer.Id);
        await AssertMessageAsync(
            await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{plan.Id}/update", ValidSupplementRequest()),
            HttpStatusCode.NotFound,
            Messages.DidntFind);

        Assert.That((await GetSupplementPlanSnapshotsAsync(trainee.Id)), Is.EqualTo(before));

        await AssertMessageAsync(await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{plan.Id}/assign", null), HttpStatusCode.NotFound, Messages.DidntFind);
        Assert.That((await GetSupplementPlanSnapshotsAsync(trainee.Id)), Is.EqualTo(before));

        await AssertMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{plan.Id}/delete", null),
            HttpStatusCode.NotFound,
            Messages.DidntFind);

        Assert.That((await GetSupplementPlanSnapshotsAsync(trainee.Id)), Is.EqualTo(before));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans/{planId}/update", "trainer-shared", "former-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans/{planId}/delete", "trainer-shared", "former-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans/{planId}/assign", "trainer-shared", "former-relationship-denial")]
    public async Task SupplementUpdate_AfterUnlink_ReturnsNotFoundAndPreservesPlans()
    {
        var trainer = await SeedTrainerAsync("task10-former-supp-update-trainer", "task10-former-supp-update-trainer@example.com");
        var trainee = await SeedUserAsync("task10-former-supp-update-trainee", "task10-former-supp-update-trainee@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);
        var plan = await CreateSupplementPlanAsync(trainer.Id, trainee.Id, "Former plan", "Original", new SupplementPlanItemInput("Item", "1", "08:00", 127, 0));
        var before = await GetSupplementPlanSnapshotsAsync(trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        using (var unlinkResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/unlink", null))
        {
            Assert.That(unlinkResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        await AssertMessageAsync(
            await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{plan.Id}/update", ValidSupplementRequest()),
            HttpStatusCode.NotFound,
            Messages.DidntFind);

        Assert.That((await GetSupplementPlanSnapshotsAsync(trainee.Id)), Is.EqualTo(before));

        await AssertMessageAsync(await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{plan.Id}/assign", null), HttpStatusCode.NotFound, Messages.DidntFind);
        Assert.That((await GetSupplementPlanSnapshotsAsync(trainee.Id)), Is.EqualTo(before));

        await AssertMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{plan.Id}/delete", null),
            HttpStatusCode.NotFound,
            Messages.DidntFind);

        Assert.That((await GetSupplementPlanSnapshotsAsync(trainee.Id)), Is.EqualTo(before));
    }

    [Test]
    public async Task AssignAndUnassign_PreserveSingleActiveAndNoOpSemantics()
    {
        var trainer = await SeedTrainerAsync("trainer-supp-assignment", "trainer-supp-assignment@example.com");
        var otherTrainer = await SeedTrainerAsync("trainer-supp-assignment-other", "trainer-supp-assignment-other@example.com");
        var trainee = await SeedUserAsync("trainee-supp-assignment", "trainee-supp-assignment@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        var first = await CreateSupplementPlanAsync(
            trainer.Id,
            trainee.Id,
            "First",
            "First notes",
            new SupplementPlanItemInput("First item", "1", "08:00", 127, 0));
        var second = await CreateSupplementPlanAsync(
            trainer.Id,
            trainee.Id,
            "Second",
            "Second notes",
            new SupplementPlanItemInput("Second item", "1", "09:00", 127, 0));
        var inactive = await CreateSupplementPlanAsync(
            trainer.Id,
            trainee.Id,
            "Inactive",
            "Inactive notes",
            new SupplementPlanItemInput("Inactive item", "1", "10:00", 127, 0));

        SetAuthorizationHeader(trainer.Id);
        await AssertMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{first.Id}/assign", null),
            HttpStatusCode.OK,
            Messages.Updated);
        await AssertSingleActivePlanAsync(trainee.Id, first.Id);

        await AssertMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{second.Id}/assign", null),
            HttpStatusCode.OK,
            Messages.Updated);
        await AssertSingleActivePlanAsync(trainee.Id, second.Id);

        var activePlans = await GetSupplementPlansIgnoringFiltersAsync(trainee.Id);
        Assert.That(activePlans.Single(plan => plan.Id.ToString() == first.Id).IsActive, Is.False);
        Assert.That(activePlans.Single(plan => plan.Id.ToString() == inactive.Id).IsActive, Is.False);

        await AssertMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/unassign", null),
            HttpStatusCode.OK,
            Messages.Updated);
        Assert.That(((await GetSupplementPlansIgnoringFiltersAsync(trainee.Id))).Any(plan => plan.IsActive), Is.False);

        var noActiveSnapshot = await GetSupplementPlanSnapshotsAsync(trainee.Id);
        await AssertMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/unassign", null),
            HttpStatusCode.OK,
            Messages.Updated);
        Assert.That((await GetSupplementPlanSnapshotsAsync(trainee.Id)), Is.EqualTo(noActiveSnapshot));

        var foreignPlanId = await SeedSupplementPlanAsync(otherTrainer.Id, trainee.Id, "Foreign active", isActive: true);
        var foreignBefore = (await GetSupplementPlanSnapshotsAsync(trainee.Id))
            .Single(plan => plan.Id == foreignPlanId.ToString());

        await AssertMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/unassign", null),
            HttpStatusCode.OK,
            Messages.Updated);

        var foreignAfter = (await GetSupplementPlanSnapshotsAsync(trainee.Id))
            .Single(plan => plan.Id == foreignPlanId.ToString());
        Assert.That(foreignAfter, Is.EqualTo(foreignBefore));
        Assert.That(foreignAfter.IsActive, Is.True);
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans/unassign", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans/unassign", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans/unassign", "trainer-shared", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans/unassign", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans/unassign", "trainer-shared", "former-relationship-denial")]
    public async Task UnassignTraineePlan_EnforcesRelationshipAndOwnershipBoundaries()
    {
        var trainer = await SeedTrainerAsync("task10-unassign-owner", "task10-unassign-owner@example.com");
        var foreignTrainer = await SeedTrainerAsync("task10-unassign-foreign", "task10-unassign-foreign@example.com");
        var unrelatedTrainer = await SeedTrainerAsync("task10-unassign-unrelated", "task10-unassign-unrelated@example.com");
        var trainee = await SeedUserAsync("task10-unassign-trainee", "task10-unassign-trainee@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);
        await LinkTrainerAndTraineeAsync(foreignTrainer.Id, trainee.Id);

        var ownedPlanId = await SeedSupplementPlanAsync(trainer.Id, trainee.Id, "Owned active", isActive: true);

        SetAuthorizationHeader(trainer.Id);
        await AssertMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/unassign", null),
            HttpStatusCode.OK,
            Messages.Updated);
        Assert.That((await GetSupplementPlanSnapshotsAsync(trainee.Id))
            .Single(plan => plan.Id == ownedPlanId.ToString())
            .IsActive, Is.False);

        var foreignPlanId = await SeedSupplementPlanAsync(foreignTrainer.Id, trainee.Id, "Foreign active", isActive: true);
        var beforeDeniedOrNoOpRequests = await GetSupplementPlanSnapshotsAsync(trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        await AssertMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/unassign", null),
            HttpStatusCode.OK,
            Messages.Updated);
        Assert.That((await GetSupplementPlanSnapshotsAsync(trainee.Id)), Is.EqualTo(beforeDeniedOrNoOpRequests));
        Assert.That(beforeDeniedOrNoOpRequests.Single(plan => plan.Id == foreignPlanId.ToString()).IsActive, Is.True);

        SetAuthorizationHeader(unrelatedTrainer.Id);
        await AssertMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/unassign", null),
            HttpStatusCode.NotFound,
            Messages.DidntFind);
        Assert.That((await GetSupplementPlanSnapshotsAsync(trainee.Id)), Is.EqualTo(beforeDeniedOrNoOpRequests));

        ClearAuthorizationHeader();
        using (var anonymousResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/unassign", null))
        {
            Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        Assert.That((await GetSupplementPlanSnapshotsAsync(trainee.Id)), Is.EqualTo(beforeDeniedOrNoOpRequests));

        SetAuthorizationHeader(trainer.Id);
        using (var unlinkResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/unlink", null))
        {
            Assert.That(unlinkResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        await AssertMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/unassign", null),
            HttpStatusCode.NotFound,
            Messages.DidntFind);
        Assert.That((await GetSupplementPlanSnapshotsAsync(trainee.Id)), Is.EqualTo(beforeDeniedOrNoOpRequests));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/supplement-plans", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/supplement-plans", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/supplement-plans", "trainer-shared", "anonymous-denial")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/supplement-plans", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/supplement-plans", "trainer-shared", "former-relationship-denial")]
    public async Task SupplementPlanList_EnforcesRelationshipAndOwnershipBoundaries()
    {
        var owner = await SeedTrainerAsync("task10-supplement-list-owner", "task10-supplement-list-owner@example.com");
        var foreignTrainer = await SeedTrainerAsync("task10-supplement-list-foreign", "task10-supplement-list-foreign@example.com");
        var unrelatedTrainer = await SeedTrainerAsync("task10-supplement-list-unrelated", "task10-supplement-list-unrelated@example.com");
        var trainee = await SeedUserAsync("task10-supplement-list-trainee", "task10-supplement-list-trainee@example.com", "password123");
        await LinkTrainerAndTraineeAsync(owner.Id, trainee.Id);
        await LinkTrainerAndTraineeAsync(foreignTrainer.Id, trainee.Id);
        var planId = await SeedSupplementPlanAsync(owner.Id, trainee.Id, "Owner-only plan", isActive: true);
        var before = await GetSupplementPlanSnapshotNoTrackingAsync(planId);

        SetAuthorizationHeader(owner.Id);
        using (var ownerResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans"))
        {
            Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            using var ownerPlans = await ReadJsonAsync(ownerResponse);
            Assert.That(ownerPlans.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(ownerPlans.RootElement.EnumerateArray()
                .Select(plan => plan.GetProperty("_id").GetString()), Does.Contain(planId.ToString()));
        }

        Assert.That((await GetSupplementPlanSnapshotNoTrackingAsync(planId)), Is.EqualTo(before));

        SetAuthorizationHeader(foreignTrainer.Id);
        using (var foreignResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans"))
        {
            Assert.That(foreignResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var foreignBody = await foreignResponse.Content.ReadAsStringAsync();
            var assertedChain19 = foreignBody;
            Assert.That(assertedChain19, Does.Not.Contain(planId.ToString()));
            Assert.That(assertedChain19, Does.Not.Contain("Owner-only plan"));
            using var foreignPlans = JsonDocument.Parse(foreignBody);
            Assert.That(foreignPlans.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(foreignPlans.RootElement.GetArrayLength(), Is.EqualTo(0));
        }

        Assert.That((await GetSupplementPlanSnapshotNoTrackingAsync(planId)), Is.EqualTo(before));

        SetAuthorizationHeader(unrelatedTrainer.Id);
        using (var unrelatedResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans"))
        {
            Assert.That(unrelatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            var unrelatedBody = await unrelatedResponse.Content.ReadAsStringAsync();
            var assertedChain20 = unrelatedBody;
            Assert.That(assertedChain20, Does.Not.Contain(planId.ToString()));
            Assert.That(assertedChain20, Does.Not.Contain("Owner-only plan"));
            using var message = JsonDocument.Parse(unrelatedBody);
            Assert.That(message.RootElement.GetProperty("msg").GetString(), Is.EqualTo(Messages.DidntFind));
        }

        Assert.That((await GetSupplementPlanSnapshotNoTrackingAsync(planId)), Is.EqualTo(before));

        ClearAuthorizationHeader();
        using (var anonymousResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans"))
        {
            Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            var anonymousBody = await anonymousResponse.Content.ReadAsStringAsync();
            var assertedChain21 = anonymousBody;
            Assert.That(assertedChain21, Does.Not.Contain(planId.ToString()));
            Assert.That(assertedChain21, Does.Not.Contain("Owner-only plan"));
        }

        Assert.That((await GetSupplementPlanSnapshotNoTrackingAsync(planId)), Is.EqualTo(before));

        SetAuthorizationHeader(owner.Id);
        using (var unlinkResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/unlink", null))
        {
            Assert.That(unlinkResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        using (var formerResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans"))
        {
            Assert.That(formerResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            var formerBody = await formerResponse.Content.ReadAsStringAsync();
            var assertedChain22 = formerBody;
            Assert.That(assertedChain22, Does.Not.Contain(planId.ToString()));
            Assert.That(assertedChain22, Does.Not.Contain("Owner-only plan"));
            using var message = JsonDocument.Parse(formerBody);
            Assert.That(message.RootElement.GetProperty("msg").GetString(), Is.EqualTo(Messages.DidntFind));
        }

        Assert.That((await GetSupplementPlanSnapshotNoTrackingAsync(planId)), Is.EqualTo(before));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/supplements/compliance", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/supplements/compliance", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/supplements/compliance", "trainer-shared", "anonymous-denial")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/supplements/compliance", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/supplements/compliance", "trainer-shared", "former-relationship-denial")]
    public async Task SupplementCompliance_EnforcesRelationshipAndOwnershipBoundaries()
    {
        var owner = await SeedTrainerAsync("task10-supplement-compliance-owner", "task10-supplement-compliance-owner@example.com");
        var foreignTrainer = await SeedTrainerAsync("task10-supplement-compliance-foreign", "task10-supplement-compliance-foreign@example.com");
        var unrelatedTrainer = await SeedTrainerAsync("task10-supplement-compliance-unrelated", "task10-supplement-compliance-unrelated@example.com");
        var trainee = await SeedUserAsync("task10-supplement-compliance-trainee", "task10-supplement-compliance-trainee@example.com", "password123");
        var date = new DateOnly(2026, 8, 5);
        await LinkTrainerAndTraineeAsync(owner.Id, trainee.Id);
        await LinkTrainerAndTraineeAsync(foreignTrainer.Id, trainee.Id);
        var planId = await SeedSupplementPlanAsync(owner.Id, trainee.Id, "Owner-only compliance plan", isActive: true);
        var before = await GetSupplementPlanSnapshotNoTrackingAsync(planId);
        var route = $"/api/trainer/trainees/{trainee.Id}/supplements/compliance?fromDate={date:yyyy-MM-dd}&toDate={date:yyyy-MM-dd}";

        SetAuthorizationHeader(owner.Id);
        using (var ownerResponse = await Client.GetAsync(route))
        {
            Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            using var ownerSummary = await ReadJsonAsync(ownerResponse);
            AssertComplianceContract(ownerSummary.RootElement, trainee.Id.ToString(), date, date, 1, 0, 0);
        }

        Assert.That((await GetSupplementPlanSnapshotNoTrackingAsync(planId)), Is.EqualTo(before));

        SetAuthorizationHeader(foreignTrainer.Id);
        using (var foreignResponse = await Client.GetAsync(route))
        {
            Assert.That(foreignResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var foreignBody = await foreignResponse.Content.ReadAsStringAsync();
            var assertedChain23 = foreignBody;
            Assert.That(assertedChain23, Does.Not.Contain(planId.ToString()));
            Assert.That(assertedChain23, Does.Not.Contain("Owner-only compliance plan"));
            using var foreignSummary = JsonDocument.Parse(foreignBody);
            AssertComplianceContract(foreignSummary.RootElement, trainee.Id.ToString(), date, date, 0, 0, 0);
        }

        Assert.That((await GetSupplementPlanSnapshotNoTrackingAsync(planId)), Is.EqualTo(before));

        SetAuthorizationHeader(unrelatedTrainer.Id);
        using (var unrelatedResponse = await Client.GetAsync(route))
        {
            Assert.That(unrelatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            var unrelatedBody = await unrelatedResponse.Content.ReadAsStringAsync();
            var assertedChain24 = unrelatedBody;
            Assert.That(assertedChain24, Does.Not.Contain(planId.ToString()));
            Assert.That(assertedChain24, Does.Not.Contain("Owner-only compliance plan"));
            using var message = JsonDocument.Parse(unrelatedBody);
            Assert.That(message.RootElement.GetProperty("msg").GetString(), Is.EqualTo(Messages.DidntFind));
        }

        Assert.That((await GetSupplementPlanSnapshotNoTrackingAsync(planId)), Is.EqualTo(before));

        ClearAuthorizationHeader();
        using (var anonymousResponse = await Client.GetAsync(route))
        {
            Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            var anonymousBody = await anonymousResponse.Content.ReadAsStringAsync();
            var assertedChain25 = anonymousBody;
            Assert.That(assertedChain25, Does.Not.Contain(planId.ToString()));
            Assert.That(assertedChain25, Does.Not.Contain("Owner-only compliance plan"));
        }

        Assert.That((await GetSupplementPlanSnapshotNoTrackingAsync(planId)), Is.EqualTo(before));

        SetAuthorizationHeader(owner.Id);
        using (var unlinkResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/unlink", null))
        {
            Assert.That(unlinkResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        using (var formerResponse = await Client.GetAsync(route))
        {
            Assert.That(formerResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            var formerBody = await formerResponse.Content.ReadAsStringAsync();
            var assertedChain26 = formerBody;
            Assert.That(assertedChain26, Does.Not.Contain(planId.ToString()));
            Assert.That(assertedChain26, Does.Not.Contain("Owner-only compliance plan"));
            using var message = JsonDocument.Parse(formerBody);
            Assert.That(message.RootElement.GetProperty("msg").GetString(), Is.EqualTo(Messages.DidntFind));
        }

        Assert.That((await GetSupplementPlanSnapshotNoTrackingAsync(planId)), Is.EqualTo(before));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans", "trainer-shared", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/supplement-plans", "trainer-shared", "former-relationship-denial")]
    public async Task SupplementPlanCreate_EnforcesRelationshipAndOwnershipBoundaries()
    {
        var owner = await SeedTrainerAsync("task10-supplement-create-owner", "task10-supplement-create-owner@example.com");
        var foreignTrainer = await SeedTrainerAsync("task10-supplement-create-foreign", "task10-supplement-create-foreign@example.com");
        var unrelatedTrainer = await SeedTrainerAsync("task10-supplement-create-unrelated", "task10-supplement-create-unrelated@example.com");
        var trainee = await SeedUserAsync("task10-supplement-create-trainee", "task10-supplement-create-trainee@example.com", "password123");
        var route = $"/api/trainer/trainees/{trainee.Id}/supplement-plans";
        await LinkTrainerAndTraineeAsync(owner.Id, trainee.Id);
        await LinkTrainerAndTraineeAsync(foreignTrainer.Id, trainee.Id);
        var ownerPlanId = await SeedSupplementPlanAsync(owner.Id, trainee.Id, "Existing owner plan", isActive: true);
        var ownerPlanBefore = await GetSupplementPlanSnapshotNoTrackingAsync(ownerPlanId);

        SetAuthorizationHeader(owner.Id);
        using (var ownerResponse = await Client.PostAsJsonAsync(route, new
        {
            name = "Owner-created plan",
            notes = "Owner-created notes",
            items = new[] { new { supplementName = "Owner item", dosage = "1", timeOfDay = "08:00", daysOfWeekMask = 127, order = 0 } }
        }))
        {
            Assert.That(ownerResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            using var ownerCreated = await ReadJsonAsync(ownerResponse);
            Assert.That(ownerCreated.RootElement.GetProperty("trainerId").GetString(), Is.EqualTo(owner.Id.ToString()));
            Assert.That(ownerCreated.RootElement.GetProperty("traineeId").GetString(), Is.EqualTo(trainee.Id.ToString()));
            Assert.That(ownerCreated.RootElement.GetProperty("name").GetString(), Is.EqualTo("Owner-created plan"));
        }

        Assert.That((await GetSupplementPlanSnapshotNoTrackingAsync(ownerPlanId)), Is.EqualTo(ownerPlanBefore));

        SetAuthorizationHeader(foreignTrainer.Id);
        using (var foreignResponse = await Client.PostAsJsonAsync(route, new
        {
            name = "Foreign-created plan",
            notes = "Foreign-created notes",
            items = new[] { new { supplementName = "Foreign item", dosage = "1", timeOfDay = "08:00", daysOfWeekMask = 127, order = 0 } }
        }))
        {
            Assert.That(foreignResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            using var foreignCreated = await ReadJsonAsync(foreignResponse);
            Assert.That(foreignCreated.RootElement.GetProperty("trainerId").GetString(), Is.EqualTo(foreignTrainer.Id.ToString()));
            Assert.That(foreignCreated.RootElement.GetProperty("traineeId").GetString(), Is.EqualTo(trainee.Id.ToString()));
            Assert.That(foreignCreated.RootElement.GetProperty("name").GetString(), Is.EqualTo("Foreign-created plan"));
            var assertedChain27 = foreignCreated.RootElement.GetRawText();
            Assert.That(assertedChain27, Does.Not.Contain(ownerPlanId.ToString()));
            Assert.That(assertedChain27, Does.Not.Contain("Existing owner plan"));
        }

        Assert.That((await GetSupplementPlanSnapshotNoTrackingAsync(ownerPlanId)), Is.EqualTo(ownerPlanBefore));
        var beforeDeniedRequests = await GetSupplementPlanCountNoTrackingAsync(trainee.Id);

        SetAuthorizationHeader(unrelatedTrainer.Id);
        using (var unrelatedResponse = await Client.PostAsJsonAsync(route, ValidSupplementRequest()))
        {
            Assert.That(unrelatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            var unrelatedBody = await unrelatedResponse.Content.ReadAsStringAsync();
            var assertedChain28 = unrelatedBody;
            Assert.That(assertedChain28, Does.Not.Contain(ownerPlanId.ToString()));
            Assert.That(assertedChain28, Does.Not.Contain("Existing owner plan"));
            using var message = JsonDocument.Parse(unrelatedBody);
            Assert.That(message.RootElement.GetProperty("msg").GetString(), Is.EqualTo(Messages.DidntFind));
        }

        Assert.That((await GetSupplementPlanCountNoTrackingAsync(trainee.Id)), Is.EqualTo(beforeDeniedRequests));
        Assert.That((await GetSupplementPlanSnapshotNoTrackingAsync(ownerPlanId)), Is.EqualTo(ownerPlanBefore));

        ClearAuthorizationHeader();
        using (var anonymousResponse = await Client.PostAsJsonAsync(route, ValidSupplementRequest()))
        {
            Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            var anonymousBody = await anonymousResponse.Content.ReadAsStringAsync();
            var assertedChain29 = anonymousBody;
            Assert.That(assertedChain29, Does.Not.Contain(ownerPlanId.ToString()));
            Assert.That(assertedChain29, Does.Not.Contain("Existing owner plan"));
        }

        Assert.That((await GetSupplementPlanCountNoTrackingAsync(trainee.Id)), Is.EqualTo(beforeDeniedRequests));
        Assert.That((await GetSupplementPlanSnapshotNoTrackingAsync(ownerPlanId)), Is.EqualTo(ownerPlanBefore));

        SetAuthorizationHeader(owner.Id);
        using (var unlinkResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/unlink", null))
        {
            Assert.That(unlinkResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        using (var formerResponse = await Client.PostAsJsonAsync(route, ValidSupplementRequest()))
        {
            Assert.That(formerResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            var formerBody = await formerResponse.Content.ReadAsStringAsync();
            var assertedChain30 = formerBody;
            Assert.That(assertedChain30, Does.Not.Contain(ownerPlanId.ToString()));
            Assert.That(assertedChain30, Does.Not.Contain("Existing owner plan"));
            using var message = JsonDocument.Parse(formerBody);
            Assert.That(message.RootElement.GetProperty("msg").GetString(), Is.EqualTo(Messages.DidntFind));
        }

        Assert.That((await GetSupplementPlanCountNoTrackingAsync(trainee.Id)), Is.EqualTo(beforeDeniedRequests));
        Assert.That((await GetSupplementPlanSnapshotNoTrackingAsync(ownerPlanId)), Is.EqualTo(ownerPlanBefore));
    }

    [Test]
    public async Task Schedule_PreservesMaskOrderingAndEmptyResult()
    {
        var trainer = await SeedTrainerAsync("trainer-supp-schedule", "trainer-supp-schedule@example.com");
        var trainee = await SeedUserAsync("trainee-supp-schedule", "trainee-supp-schedule@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);
        var monday = new DateOnly(2026, 7, 6);

        var plan = await CreateSupplementPlanAsync(
            trainer.Id,
            trainee.Id,
            "Schedule stack",
            "Schedule notes",
            new SupplementPlanItemInput("Later", "1", "20:00", 1, 2),
            new SupplementPlanItemInput("Tuesday only", "1", "07:00", 2, 0),
            new SupplementPlanItemInput("Zinc", "1", "09:00", 1, 1),
            new SupplementPlanItemInput("Iron", "1", "08:00", 1, 1),
            new SupplementPlanItemInput("Daily", "1", "07:00", 127, 0));

        SetAuthorizationHeader(trainer.Id);
        await AssertMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{plan.Id}/assign", null),
            HttpStatusCode.OK,
            Messages.Updated);

        var iron = plan.Items.Single(item => item.SupplementName == "Iron");
        var takenAt = new DateTimeOffset(2026, 7, 6, 8, 5, 0, TimeSpan.Zero);
        SetAuthorizationHeader(trainee.Id);
        var checkOff = await Client.PostAsJsonAsync("/api/trainee/supplements/intakes/check-off", new
        {
            planItemId = iron.Id,
            intakeDate = monday,
            takenAt
        });
        Assert.That(checkOff.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var scheduledResponse = await Client.GetAsync($"/api/trainee/supplements/schedule?date={monday:yyyy-MM-dd}");
        Assert.That(scheduledResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var schedule = await ReadJsonAsync(scheduledResponse))
        {
            Assert.That(schedule.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(schedule.RootElement.EnumerateArray().Select(item => item.GetProperty("supplementName").GetString()), Is.EqualTo(new[] { "Daily", "Iron", "Zinc", "Later" }));
            Assert.That((schedule.RootElement.EnumerateArray()).Any(item => item.GetProperty("supplementName").GetString() == "Tuesday only"), Is.False);

            foreach (var entry in schedule.RootElement.EnumerateArray())
            {
                var name = entry.GetProperty("supplementName").GetString();
                AssertScheduleContract(
                    entry,
                    monday,
                    name == "Iron",
                    name == "Iron" ? takenAt : null);
            }
        }

        var beforeDefaultDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var defaultScheduleResponse = await Client.GetAsync("/api/trainee/supplements/schedule");
        var afterDefaultDate = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.That(defaultScheduleResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var defaultSchedule = await ReadJsonAsync(defaultScheduleResponse))
        {
            Assert.That((defaultSchedule.RootElement.EnumerateArray()).Any(entry =>
                entry.GetProperty("supplementName").GetString() == "Daily"), Is.True);
            foreach (var entry in defaultSchedule.RootElement.EnumerateArray())
            {
                Assert.That(DateOnly.Parse(entry.GetProperty("intakeDate").GetString()!), Is.AnyOf(beforeDefaultDate, afterDefaultDate));
            }
        }

        var emptyTrainee = await SeedUserAsync("trainee-supp-empty-schedule", "trainee-supp-empty-schedule@example.com", "password123");
        SetAuthorizationHeader(emptyTrainee.Id);
        var emptyResponse = await Client.GetAsync($"/api/trainee/supplements/schedule?date={monday:yyyy-MM-dd}");
        Assert.That(emptyResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var empty = await ReadJsonAsync(emptyResponse);
        Assert.That(empty.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(empty.RootElement.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task CheckOff_PreservesExistingAndNewLogSemantics()
    {
        var trainer = await SeedTrainerAsync("trainer-supp-check-off", "trainer-supp-check-off@example.com");
        var trainee = await SeedUserAsync("trainee-supp-check-off", "trainee-supp-check-off@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);
        var intakeDate = new DateOnly(2026, 7, 7);
        var plan = await CreateSupplementPlanAsync(
            trainer.Id,
            trainee.Id,
            "Check-off stack",
            "Check-off notes",
            new SupplementPlanItemInput("Explicit", "1", "08:00", 127, 0),
            new SupplementPlanItemInput("Default", "1", "09:00", 127, 1));

        SetAuthorizationHeader(trainer.Id);
        await AssertMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{plan.Id}/assign", null),
            HttpStatusCode.OK,
            Messages.Updated);

        var explicitItem = plan.Items.Single(item => item.SupplementName == "Explicit");
        var defaultItem = plan.Items.Single(item => item.SupplementName == "Default");
        var firstTakenAt = new DateTimeOffset(2026, 7, 7, 8, 5, 0, TimeSpan.Zero);
        var replacementTakenAt = new DateTimeOffset(2026, 7, 7, 8, 10, 0, TimeSpan.Zero);
        SetAuthorizationHeader(trainee.Id);

        var explicitResponse = await Client.PostAsJsonAsync("/api/trainee/supplements/intakes/check-off", new
        {
            planItemId = explicitItem.Id,
            intakeDate,
            takenAt = firstTakenAt
        });
        Assert.That(explicitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var explicitEntry = await ReadJsonAsync(explicitResponse))
        {
            AssertScheduleContract(explicitEntry.RootElement, intakeDate, taken: true, expectedTakenAt: firstTakenAt);
        }

        var retainedResponse = await Client.PostAsJsonAsync("/api/trainee/supplements/intakes/check-off", new
        {
            planItemId = explicitItem.Id,
            intakeDate
        });
        Assert.That(retainedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var retainedEntry = await ReadJsonAsync(retainedResponse))
        {
            AssertScheduleContract(retainedEntry.RootElement, intakeDate, taken: true, expectedTakenAt: firstTakenAt);
        }

        var replacedResponse = await Client.PostAsJsonAsync("/api/trainee/supplements/intakes/check-off", new
        {
            planItemId = explicitItem.Id,
            intakeDate,
            takenAt = replacementTakenAt
        });
        Assert.That(replacedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var replacedEntry = await ReadJsonAsync(replacedResponse))
        {
            AssertScheduleContract(replacedEntry.RootElement, intakeDate, taken: true, expectedTakenAt: replacementTakenAt);
        }

        var beforeDefaultTakenAt = DateTimeOffset.UtcNow;
        var defaultResponse = await Client.PostAsJsonAsync("/api/trainee/supplements/intakes/check-off", new
        {
            planItemId = defaultItem.Id,
            intakeDate
        });
        var afterDefaultTakenAt = DateTimeOffset.UtcNow;
        Assert.That(defaultResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var defaultEntry = await ReadJsonAsync(defaultResponse))
        {
            AssertScheduleContract(defaultEntry.RootElement, intakeDate, taken: true, expectedTakenAt: null);
            var generatedTakenAt = defaultEntry.RootElement.GetProperty("takenAt").GetDateTimeOffset();
            var assertedChain31 = generatedTakenAt;
            Assert.That(assertedChain31, Is.GreaterThanOrEqualTo(beforeDefaultTakenAt));
            Assert.That(assertedChain31, Is.LessThanOrEqualTo(afterDefaultTakenAt));
        }

        var logs = await GetSupplementIntakeLogsAsync(trainee.Id);
        Assert.That(logs, Has.Exactly(2).Items);
        Assert.That(logs.Single(log => log.PlanItemId.ToString() == explicitItem.Id).TakenAt, Is.EqualTo(replacementTakenAt));
        var assertedChain32 = logs.Single(log => log.PlanItemId.ToString() == defaultItem.Id).TakenAt;
        Assert.That(assertedChain32, Is.GreaterThanOrEqualTo(beforeDefaultTakenAt));
        Assert.That(assertedChain32, Is.LessThanOrEqualTo(afterDefaultTakenAt));
    }

    [Test]
    public async Task Compliance_PreservesInclusiveRoundingAndZeroSummary()
    {
        var trainer = await SeedTrainerAsync("trainer-supp-compliance", "trainer-supp-compliance@example.com");
        var trainee = await SeedUserAsync("trainee-supp-compliance", "trainee-supp-compliance@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);
        var fromDate = new DateOnly(2026, 7, 6);
        var toDate = new DateOnly(2026, 7, 8);
        var plan = await CreateSupplementPlanAsync(
            trainer.Id,
            trainee.Id,
            "Compliance stack",
            "Compliance notes",
            new SupplementPlanItemInput("Daily", "1", "08:00", 127, 0));

        SetAuthorizationHeader(trainer.Id);
        await AssertMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{plan.Id}/assign", null),
            HttpStatusCode.OK,
            Messages.Updated);

        SetAuthorizationHeader(trainee.Id);
        var checkOffResponse = await Client.PostAsJsonAsync("/api/trainee/supplements/intakes/check-off", new
        {
            planItemId = plan.Items.Single().Id,
            intakeDate = fromDate,
            takenAt = new DateTimeOffset(2026, 7, 6, 8, 0, 0, TimeSpan.Zero)
        });
        Assert.That(checkOffResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        SetAuthorizationHeader(trainer.Id);
        var summaryResponse = await Client.GetAsync(
            $"/api/trainer/trainees/{trainee.Id}/supplements/compliance?fromDate={fromDate:yyyy-MM-dd}&toDate={toDate:yyyy-MM-dd}");
        Assert.That(summaryResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var summary = await ReadJsonAsync(summaryResponse))
        {
            AssertComplianceContract(summary.RootElement, trainee.Id.ToString(), fromDate, toDate, 3, 1, 33.33);
        }

        await AssertMessageAsync(
            await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/supplements/compliance?fromDate={toDate:yyyy-MM-dd}&toDate={fromDate:yyyy-MM-dd}"),
            HttpStatusCode.BadRequest,
            Messages.InvalidDateRange);
        await AssertMessageAsync(
            await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/supplements/compliance?fromDate={fromDate:yyyy-MM-dd}&toDate={fromDate.AddDays(366):yyyy-MM-dd}"),
            HttpStatusCode.BadRequest,
            Messages.DateRangeTooLarge);
        await AssertMessageAsync(
            await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/supplements/compliance"),
            HttpStatusCode.BadRequest,
            Messages.DateRangeRequired);

        await AssertMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/unassign", null),
            HttpStatusCode.OK,
            Messages.Updated);
        var zeroSummaryResponse = await Client.GetAsync(
            $"/api/trainer/trainees/{trainee.Id}/supplements/compliance?fromDate={fromDate:yyyy-MM-dd}&toDate={toDate:yyyy-MM-dd}");
        Assert.That(zeroSummaryResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var zeroSummary = await ReadJsonAsync(zeroSummaryResponse))
        {
            AssertComplianceContract(zeroSummary.RootElement, trainee.Id.ToString(), fromDate, toDate, 0, 0, 0);
        }

        SetRequestCulture("pl");
        await AssertMessageAsync(
            await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/supplements/compliance"),
            HttpStatusCode.BadRequest,
            GetMessageForCulture("pl", () => Messages.DateRangeRequired));
    }

    [Test]
    public async Task SupplementRoutes_PreserveMalformedIdAndMessageContracts()
    {
        var trainer = await SeedTrainerAsync("trainer-supp-routes", "trainer-supp-routes@example.com");
        var regularUser = await SeedUserAsync("regular-supp-routes", "regular-supp-routes@example.com", "password123");
        var existingTrainee = await SeedUserAsync("trainee-supp-routes", "trainee-supp-routes@example.com", "password123");

        SetAuthorizationHeader(trainer.Id);
        await AssertMessageAsync(
            await Client.GetAsync("/api/trainer/trainees/not-a-guid/supplement-plans"),
            HttpStatusCode.BadRequest,
            Messages.UserIdRequired);
        await AssertMessageAsync(
            await Client.PostAsJsonAsync("/api/trainer/trainees/not-a-guid/supplement-plans", ValidSupplementRequest()),
            HttpStatusCode.BadRequest,
            Messages.UserIdRequired);

        foreach (var action in new[] { "update", "delete", "assign" })
        {
            var route = $"/api/trainer/trainees/{existingTrainee.Id}/supplement-plans/not-a-guid/{action}";
            var response = action == "update"
                ? await Client.PostAsJsonAsync(route, ValidSupplementRequest())
                : await Client.PostAsync(route, null);
            await AssertMessageAsync(response, HttpStatusCode.BadRequest, Messages.FieldRequired);
        }

        await AssertMessageAsync(
            await Client.PostAsync("/api/trainer/trainees/not-a-guid/supplement-plans/unassign", null),
            HttpStatusCode.BadRequest,
            Messages.UserIdRequired);
        await AssertMessageAsync(
            await Client.GetAsync("/api/trainer/trainees/not-a-guid/supplements/compliance"),
            HttpStatusCode.BadRequest,
            Messages.UserIdRequired);

        SetAuthorizationHeader(existingTrainee.Id);
        await AssertMessageAsync(
            await Client.PostAsJsonAsync("/api/trainee/supplements/intakes/check-off", new
            {
                planItemId = "not-a-guid",
                intakeDate = new DateOnly(2026, 7, 6)
            }),
            HttpStatusCode.BadRequest,
            Messages.FieldRequired);

        SetAuthorizationHeader(regularUser.Id);
        var regularUserResponse = await Client.GetAsync($"/api/trainer/trainees/{existingTrainee.Id}/supplement-plans");
        Assert.That(regularUserResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        SetAuthorizationHeader(trainer.Id);
        await AssertMessageAsync(
            await Client.GetAsync($"/api/trainer/trainees/{existingTrainee.Id}/supplement-plans"),
            HttpStatusCode.NotFound,
            Messages.DidntFind);

        SetRequestCulture("pl");
        await AssertMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{existingTrainee.Id}/supplement-plans/not-a-guid/delete", null),
            HttpStatusCode.BadRequest,
            GetMessageForCulture("pl", () => Messages.FieldRequired));
    }

    [Test]
    public async Task SupplementFailures_DoNotPersist()
    {
        var trainer = await SeedTrainerAsync("trainer-supp-failures", "trainer-supp-failures@example.com");
        var trainee = await SeedUserAsync("trainee-supp-failures", "trainee-supp-failures@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        await AssertMessageAsync(
            await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans", new
            {
                name = "Invalid stack",
                notes = "Invalid notes",
                items = Array.Empty<object>()
            }),
            HttpStatusCode.BadRequest,
            Messages.FieldRequired);
        Assert.That((await GetSupplementPlansIgnoringFiltersAsync(trainee.Id)), Is.Empty);

        var plan = await CreateSupplementPlanAsync(
            trainer.Id,
            trainee.Id,
            "Stable stack",
            "Stable notes",
            new SupplementPlanItemInput("Stable item", "1", "08:00", 127, 0));
        await AssertMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{plan.Id}/assign", null),
            HttpStatusCode.OK,
            Messages.Updated);
        var plansBeforeFailures = await GetSupplementPlanSnapshotsAsync(trainee.Id);

        await AssertMessageAsync(
            await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/supplement-plans/{plan.Id}/update", new
            {
                name = "Rejected replacement",
                notes = "Rejected notes",
                items = new object[]
                {
                    new { supplementName = "Rejected", dosage = "1", timeOfDay = "08:00", daysOfWeekMask = 0, order = 0 }
                }
            }),
            HttpStatusCode.BadRequest,
            Messages.FieldRequired);
        Assert.That((await GetSupplementPlanSnapshotsAsync(trainee.Id)), Is.EqualTo(plansBeforeFailures));

        SetAuthorizationHeader(trainee.Id);
        await AssertMessageAsync(
            await Client.PostAsJsonAsync("/api/trainee/supplements/intakes/check-off", new
            {
                planItemId = Id<SupplementPlanItem>.New().ToString(),
                intakeDate = new DateOnly(2026, 7, 6)
            }),
            HttpStatusCode.NotFound,
            Messages.DidntFind);
        Assert.That((await GetSupplementIntakeLogsAsync(trainee.Id)), Is.Empty);

        SetAuthorizationHeader(trainer.Id);
        await AssertMessageAsync(
            await Client.PostAsync(
                $"/api/trainer/trainees/{trainee.Id}/supplement-plans/{Id<SupplementPlan>.New()}/delete",
                null),
            HttpStatusCode.NotFound,
            Messages.DidntFind);
        Assert.That((await GetSupplementPlanSnapshotsAsync(trainee.Id)), Is.EqualTo(plansBeforeFailures));
    }

    private async Task<User> SeedTrainerAsync(string name, string email)
    {
        var trainer = await SeedUserAsync(name: name, email: email, password: "password123");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alreadyLinked = await db.UserRoles.AnyAsync(ur => ur.UserId == trainer.Id && ur.RoleId == RoleSeedDataConfiguration.TrainerRoleSeedId);
        if (!alreadyLinked)
        {
            db.UserRoles.Add(LgymApi.TestUtils.TestDataFactory.ApprovedTrainerRole(db, trainer.Id));
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

    private static int MaskForDate(DateOnly date)
    {
        var normalizedDay = ((int)date.DayOfWeek + 6) % 7;
        return 1 << normalizedDay;
    }

    private async Task<SupplementPlanResponse> CreateSupplementPlanAsync(
        Id<User> trainerId,
        Id<User> traineeId,
        string name,
        string notes,
        params SupplementPlanItemInput[] items)
    {
        SetAuthorizationHeader(trainerId);
        var response = await Client.PostAsJsonAsync(
            $"/api/trainer/trainees/{traineeId}/supplement-plans",
            new
            {
                name,
                notes,
                items = items.Select(item => new
                {
                    supplementName = item.SupplementName,
                    dosage = item.Dosage,
                    timeOfDay = item.TimeOfDay,
                    daysOfWeekMask = item.DaysOfWeekMask,
                    order = item.Order
                })
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using var document = await ReadJsonAsync(response);
        AssertPlanContract(document.RootElement);
        Assert.That(document.RootElement.GetProperty("trainerId").GetString(), Is.EqualTo(trainerId.ToString()));
        Assert.That(document.RootElement.GetProperty("traineeId").GetString(), Is.EqualTo(traineeId.ToString()));
        Assert.That(document.RootElement.GetProperty("name").GetString(), Is.EqualTo(name));
        Assert.That(document.RootElement.GetProperty("notes").GetString(), Is.EqualTo(notes));
        Assert.That(document.RootElement.GetProperty("isActive").GetBoolean(), Is.False);

        return JsonSerializer.Deserialize<SupplementPlanResponse>(document.RootElement.GetRawText())!;
    }

    private async Task<List<SupplementPlan>> GetSupplementPlansIgnoringFiltersAsync(Id<User> traineeId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.SupplementPlans
            .IgnoreQueryFilters()
            .Where(plan => plan.TraineeId == traineeId)
            .Include(plan => plan.Items.OrderBy(item => item.Order).ThenBy(item => item.TimeOfDay).ThenBy(item => item.CreatedAt))
            .OrderBy(plan => plan.Name)
            .ToListAsync();
    }

    private async Task<List<SupplementIntakeLog>> GetSupplementIntakeLogsAsync(Id<User> traineeId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.SupplementIntakeLogs
            .Where(log => log.TraineeId == traineeId)
            .OrderBy(log => log.IntakeDate)
            .ThenBy(log => log.TakenAt)
            .ToListAsync();
    }

    private async Task<List<SupplementPlanSnapshot>> GetSupplementPlanSnapshotsAsync(Id<User> traineeId)
    {
        return (await GetSupplementPlansIgnoringFiltersAsync(traineeId))
            .Select(plan => new SupplementPlanSnapshot(
                plan.Id.ToString(),
                plan.IsActive,
                plan.IsDeleted,
                plan.UpdatedAt))
            .OrderBy(plan => plan.Id, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<SupplementPlanSnapshot> GetSupplementPlanSnapshotNoTrackingAsync(Id<SupplementPlan> planId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plan = await db.SupplementPlans
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == planId);

        return new SupplementPlanSnapshot(
            plan.Id.ToString(),
            plan.IsActive,
            plan.IsDeleted,
            plan.UpdatedAt);
    }

    private async Task<int> GetSupplementPlanCountNoTrackingAsync(Id<User> traineeId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.SupplementPlans
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(plan => plan.TraineeId == traineeId);
    }

    private async Task<Id<SupplementPlan>> SeedSupplementPlanAsync(
        Id<User> trainerId,
        Id<User> traineeId,
        string name,
        bool isActive)
    {
        var planId = Id<SupplementPlan>.New();
        var itemId = Id<SupplementPlanItem>.New();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.SupplementPlans.Add(new SupplementPlan
        {
            Id = planId,
            TrainerId = trainerId,
            TraineeId = traineeId,
            Name = name,
            Notes = "Seeded",
            IsActive = isActive,
            IsDeleted = false,
            Items =
            [
                new SupplementPlanItem
                {
                    Id = itemId,
                    PlanId = planId,
                    SupplementName = "Seeded item",
                    Dosage = "1",
                    DaysOfWeekMask = DaysOfWeekSet.EveryDay,
                    TimeOfDay = new TimeSpan(8, 0, 0),
                    Order = 0
                }
            ]
        });
        await db.SaveChangesAsync();

        return planId;
    }

    private async Task AssertSingleActivePlanAsync(Id<User> traineeId, string expectedPlanId)
    {
        var plans = await GetSupplementPlansIgnoringFiltersAsync(traineeId);
        Assert.That(plans.Where(plan => plan.IsActive && !plan.IsDeleted).Select(plan => plan.Id.ToString()), Is.EqualTo(new[] { expectedPlanId }));
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task AssertMessageAsync(HttpResponseMessage response, HttpStatusCode statusCode, string expectedMessage)
    {
        Assert.That(response.StatusCode, Is.EqualTo(statusCode));
        using var document = await ReadJsonAsync(response);
        Assert.That(document.RootElement.EnumerateObject().Select(property => property.Name), Is.EqualTo(new[] { "msg" }));
        Assert.That(document.RootElement.GetProperty("msg").GetString(), Is.EqualTo(expectedMessage));
        Assert.That(document.RootElement.TryGetProperty("message", out _), Is.False);
    }

    private static void AssertPlanContract(JsonElement plan)
    {
        Assert.That(plan.ValueKind, Is.EqualTo(JsonValueKind.Object));
        Assert.That(plan.EnumerateObject().Select(property => property.Name), Is.EqualTo(new[] { "_id", "trainerId", "traineeId", "name", "notes", "isActive", "createdAt", "items" }));
        Assert.That(plan.TryGetProperty("id", out _), Is.False);
        Assert.That(string.IsNullOrWhiteSpace(plan.GetProperty("_id").GetString()), Is.False);
        Assert.That(string.IsNullOrWhiteSpace(plan.GetProperty("trainerId").GetString()), Is.False);
        Assert.That(string.IsNullOrWhiteSpace(plan.GetProperty("traineeId").GetString()), Is.False);
        Assert.That(plan.GetProperty("createdAt").GetDateTimeOffset(), Is.Not.EqualTo(default(System.DateTimeOffset)));

        foreach (var item in plan.GetProperty("items").EnumerateArray())
        {
            Assert.That(item.EnumerateObject().Select(property => property.Name), Is.EqualTo(new[] { "_id", "supplementName", "dosage", "timeOfDay", "daysOfWeekMask", "order" }));
            Assert.That(item.TryGetProperty("id", out _), Is.False);
            Assert.That(string.IsNullOrWhiteSpace(item.GetProperty("_id").GetString()), Is.False);
        }
    }

    private static void AssertScheduleContract(
        JsonElement entry,
        DateOnly expectedDate,
        bool taken,
        DateTimeOffset? expectedTakenAt)
    {
        Assert.That(entry.ValueKind, Is.EqualTo(JsonValueKind.Object));
        var expectedProperties = taken
            ? new[] { "planItemId", "supplementName", "dosage", "timeOfDay", "intakeDate", "taken", "takenAt" }
            : new[] { "planItemId", "supplementName", "dosage", "timeOfDay", "intakeDate", "taken" };
        Assert.That(entry.EnumerateObject().Select(property => property.Name), Is.EqualTo(expectedProperties));
        Assert.That(string.IsNullOrWhiteSpace(entry.GetProperty("planItemId").GetString()), Is.False);
        Assert.That(entry.GetProperty("intakeDate").GetString(), Is.EqualTo(expectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        Assert.That(entry.GetProperty("taken").GetBoolean(), Is.EqualTo(taken));

        if (!taken)
        {
            Assert.That(entry.TryGetProperty("takenAt", out _), Is.False);
            return;
        }

        Assert.That(entry.GetProperty("takenAt").ValueKind, Is.EqualTo(JsonValueKind.String));
        if (expectedTakenAt is not null)
        {
            Assert.That(entry.GetProperty("takenAt").GetString(), Is.EqualTo(expectedTakenAt.Value.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture)));
        }
    }

    private static void AssertComplianceContract(
        JsonElement summary,
        string traineeId,
        DateOnly fromDate,
        DateOnly toDate,
        int plannedDoses,
        int takenDoses,
        double adherenceRate)
    {
        Assert.That(summary.ValueKind, Is.EqualTo(JsonValueKind.Object));
        Assert.That(summary.EnumerateObject().Select(property => property.Name), Is.EqualTo(new[] { "traineeId", "fromDate", "toDate", "plannedDoses", "takenDoses", "adherenceRate" }));
        Assert.That(summary.GetProperty("traineeId").GetString(), Is.EqualTo(traineeId));
        Assert.That(summary.GetProperty("fromDate").GetString(), Is.EqualTo(fromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        Assert.That(summary.GetProperty("toDate").GetString(), Is.EqualTo(toDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        Assert.That(summary.GetProperty("plannedDoses").GetInt32(), Is.EqualTo(plannedDoses));
        Assert.That(summary.GetProperty("takenDoses").GetInt32(), Is.EqualTo(takenDoses));
        Assert.That(summary.GetProperty("adherenceRate").GetDouble(), Is.EqualTo(adherenceRate));
    }

    private void SetRequestCulture(string culture)
    {
        Client.DefaultRequestHeaders.Remove("Accept-Language");
        Assert.That(Client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", culture), Is.True);
    }

    private static string GetMessageForCulture(string culture, Func<string> getMessage)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var requestedCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentCulture = requestedCulture;
            CultureInfo.CurrentUICulture = requestedCulture;
            return getMessage();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static object ValidSupplementRequest()
    {
        return new
        {
            name = "Valid stack",
            notes = "Valid notes",
            items = new object[]
            {
                new { supplementName = "Valid item", dosage = "1", timeOfDay = "08:00", daysOfWeekMask = 127, order = 0 }
            }
        };
    }

    private sealed record SupplementPlanItemInput(
        string SupplementName,
        string Dosage,
        string TimeOfDay,
        int DaysOfWeekMask,
        int Order);

    private sealed record SupplementPlanSnapshot(string Id, bool IsActive, bool IsDeleted, DateTimeOffset UpdatedAt);

    private sealed class SupplementPlanResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("trainerId")]
        public string TrainerId { get; set; } = string.Empty;

        [JsonPropertyName("traineeId")]
        public string TraineeId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("items")]
        public List<SupplementPlanItemResponse> Items { get; set; } = [];
    }

    private sealed class SupplementPlanItemResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("supplementName")]
        public string SupplementName { get; set; } = string.Empty;

        [JsonPropertyName("dosage")]
        public string Dosage { get; set; } = string.Empty;

        [JsonPropertyName("timeOfDay")]
        public string TimeOfDay { get; set; } = string.Empty;

        [JsonPropertyName("daysOfWeekMask")]
        public int DaysOfWeekMask { get; set; }

        [JsonPropertyName("order")]
        public int Order { get; set; }
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
