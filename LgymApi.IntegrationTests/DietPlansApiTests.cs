using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Notifications;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using LgymApi.IntegrationTests.Authorization;
using LgymApi.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class DietPlansApiTests : IntegrationTestBase
{
    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/diet-plans", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/history", "trainer-shared", "active-relationship-allow")]
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

        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await createResponse.Content.ReadFromJsonAsync<DietPlanResponse>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.IsActive, Is.True);

        var listResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans");
        Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var plans = await listResponse.Content.ReadFromJsonAsync<List<DietPlanResponse>>();
        Assert.That((plans).Where(x => x.Id == created.Id && x.IsActive), Has.Exactly(1).Items);

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

        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var historyResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{created.Id}/history");
        Assert.That(historyResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var history = await historyResponse.Content.ReadFromJsonAsync<List<DietPlanHistoryResponse>>();
        Assert.That(history, Is.Not.Null);
        Assert.That(history!.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(history.Select(x => x.ChangeType), Is.SupersetOf(new[] { "Created", "Updated" }));

        SetAuthorizationHeader(trainee.Id);
        var currentResponse = await Client.GetAsync("/api/trainee/diet-plan/current");
        Assert.That(currentResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var current = await currentResponse.Content.ReadFromJsonAsync<DietPlanResponse>();
        Assert.That(current, Is.Not.Null);
        Assert.That(current!.Name, Is.EqualTo("Mass phase v2"));
        Assert.That(current.Meals, Has.Exactly(2).Items);
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans", "trainer-shared", "foreign-object-denial-no-mutation")]
    public async Task TrainerCannotCreateDietForForeignTrainee()
    {
        var ownerTrainer = await SeedTrainerAsync("trainer-owner-diet", "trainer-owner-diet@example.com");
        var otherTrainer = await SeedTrainerAsync("trainer-other-diet", "trainer-other-diet@example.com");
        var trainee = await SeedUserAsync(name: "trainee-foreign-diet", email: "trainee-foreign-diet@example.com", password: "password123");
        await LinkTrainerAndTraineeAsync(ownerTrainer.Id, trainee.Id);

        SetAuthorizationHeader(otherTrainer.Id);
        var beforeCreate = await GetDietPersistenceCountsAsync();
        await AssertExactMessageAsync(
            await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans", ValidDietRequest("Foreign trainee plan")),
            HttpStatusCode.NotFound,
            EnglishMessage(() => Messages.DidntFind));

        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(beforeCreate));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans", "trainer-shared", "anonymous-denial")]
    public async Task TrainerDietCreate_WithoutRelationshipOrAuthentication_DoesNotPersist()
    {
        var unrelatedTrainer = await SeedTrainerAsync("task10-unrelated-diet-create-trainer", "task10-unrelated-diet-create-trainer@example.com");
        var trainee = await SeedUserAsync("task10-unrelated-diet-create-trainee", "task10-unrelated-diet-create-trainee@example.com", "password123");
        var beforeCreate = await GetDietPersistenceCountsAsync();

        SetAuthorizationHeader(unrelatedTrainer.Id);
        await AssertExactMessageAsync(
            await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans", ValidDietRequest("Unrelated trainee plan")),
            HttpStatusCode.NotFound,
            EnglishMessage(() => Messages.DidntFind));
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(beforeCreate));

        ClearAuthorizationHeader();
        using (var anonymousResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans", ValidDietRequest("Anonymous trainee plan")))
        {
            Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(beforeCreate));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans", "trainer-shared", "former-relationship-denial")]
    public async Task TrainerDietCreate_AfterUnlink_ReturnsNotFoundWithoutPersistence()
    {
        var trainer = await SeedTrainerAsync("task10-former-diet-create-trainer", "task10-former-diet-create-trainer@example.com");
        var trainee = await SeedUserAsync("task10-former-diet-create-trainee", "task10-former-diet-create-trainee@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        using (var unlinkResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/unlink", null))
        {
            Assert.That(unlinkResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
        var beforeCreate = await GetDietPersistenceCountsAsync();

        await AssertExactMessageAsync(
            await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans", ValidDietRequest("Former trainee plan")),
            HttpStatusCode.NotFound,
            EnglishMessage(() => Messages.DidntFind));

        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(beforeCreate));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/diet-plans", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/diet-plans", "trainer-shared", "anonymous-denial")]
    public async Task TrainerDietList_WithoutRelationshipOrAuthentication_DoesNotMutatePersistence()
    {
        var unrelatedTrainer = await SeedTrainerAsync("task10-unrelated-diet-list-trainer", "task10-unrelated-diet-list-trainer@example.com");
        var trainee = await SeedUserAsync("task10-unrelated-diet-list-trainee", "task10-unrelated-diet-list-trainee@example.com", "password123");
        var beforeRead = await GetDietPersistenceCountsAsync();

        SetAuthorizationHeader(unrelatedTrainer.Id);
        await AssertExactMessageAsync(
            await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans"),
            HttpStatusCode.NotFound,
            EnglishMessage(() => Messages.DidntFind));
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(beforeRead));

        ClearAuthorizationHeader();
        using (var anonymousResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans"))
        {
            Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(beforeRead));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/diet-plans", "trainer-shared", "foreign-object-denial-no-mutation")]
    public async Task TrainerDietList_ForForeignTrainee_ReturnsNotFoundAndPreservesPlan()
    {
        var owner = await SeedTrainerAsync("task10-foreign-diet-list-owner", "task10-foreign-diet-list-owner@example.com");
        var otherTrainer = await SeedTrainerAsync("task10-foreign-diet-list-trainer", "task10-foreign-diet-list-trainer@example.com");
        var trainee = await SeedUserAsync("task10-foreign-diet-list-trainee", "task10-foreign-diet-list-trainee@example.com", "password123");
        await LinkTrainerAndTraineeAsync(owner.Id, trainee.Id);

        SetAuthorizationHeader(owner.Id);
        var plan = await CreateDietAsync(trainee.Id, "Foreign trainer list plan", true);
        var originalPlan = await GetDietPlanSnapshotAsync(plan.Id);
        var beforeRead = await GetDietPersistenceCountsAsync();

        SetAuthorizationHeader(otherTrainer.Id);
        await AssertExactMessageAsync(
            await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans"),
            HttpStatusCode.NotFound,
            EnglishMessage(() => Messages.DidntFind));

        AssertDietPlanSnapshot((await GetDietPlanSnapshotAsync(plan.Id)), originalPlan);
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(beforeRead));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/diet-plans", "trainer-shared", "former-relationship-denial")]
    public async Task TrainerDietList_AfterUnlink_ReturnsNotFoundAndPreservesPlan()
    {
        var trainer = await SeedTrainerAsync("task10-former-diet-list-trainer", "task10-former-diet-list-trainer@example.com");
        var trainee = await SeedUserAsync("task10-former-diet-list-trainee", "task10-former-diet-list-trainee@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        var plan = await CreateDietAsync(trainee.Id, "Former trainer list plan", true);
        var originalPlan = await GetDietPlanSnapshotAsync(plan.Id);
        using (var unlinkResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/unlink", null))
        {
            Assert.That(unlinkResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
        var beforeRead = await GetDietPersistenceCountsAsync();

        await AssertExactMessageAsync(
            await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans"),
            HttpStatusCode.NotFound,
            EnglishMessage(() => Messages.DidntFind));

        AssertDietPlanSnapshot((await GetDietPlanSnapshotAsync(plan.Id)), originalPlan);
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(beforeRead));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/activate", "trainer-shared", "active-relationship-allow")]
    public async Task ActivateDietPlan_PreservesExistingActivePlanCharacterization()
    {
        var trainer = await SeedTrainerAsync("trainer-activate-diet", "trainer-activate-diet@example.com");
        var trainee = await SeedUserAsync(name: "trainee-activate-diet", email: "trainee-activate-diet@example.com", password: "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        var first = await CreateDietAsync(trainee.Id, "Diet A", true);
        var second = await CreateDietAsync(trainee.Id, "Diet B", false);
        var beforeActivation = await GetDietPersistenceCountsAsync();

        using var activateResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{second.Id}/activate", null);
        await AssertExactMessageAsync(activateResponse, HttpStatusCode.OK, EnglishMessage(() => Messages.Updated));

        var listResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans");
        var plans = await listResponse.Content.ReadFromJsonAsync<List<DietPlanResponse>>();
        Assert.That((plans).Any(x => x.IsActive && x.Id == second.Id), Is.True);
        Assert.That((plans).Any(x => x.IsActive && x.Id == first.Id), Is.True);
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo((beforeActivation.Plans, beforeActivation.Histories + 1, beforeActivation.Commands + 1)));
    }

    [Test]
    public async Task DietRoutes_PreserveMalformedIdAndMessageContracts()
    {
        var trainer = await SeedTrainerAsync("trainer-malformed-diet", "trainer-malformed-diet@example.com");
        SetAuthorizationHeader(trainer.Id);
        var validTraineeId = Id<User>.New();

        await AssertExactMessageAsync(
            await Client.GetAsync("/api/trainer/trainees/not-a-guid/diet-plans"),
            HttpStatusCode.BadRequest,
            EnglishMessage(() => Messages.UserIdRequired));
        await AssertExactMessageAsync(
            await Client.PostAsJsonAsync("/api/trainer/trainees/not-a-guid/diet-plans", ValidDietRequest()),
            HttpStatusCode.BadRequest,
            EnglishMessage(() => Messages.UserIdRequired));

        foreach (var response in new[]
                 {
                     await Client.GetAsync($"/api/trainer/trainees/not-a-guid/diet-plans/{Id<DietPlan>.New()}"),
                     await Client.PostAsJsonAsync($"/api/trainer/trainees/not-a-guid/diet-plans/{Id<DietPlan>.New()}/update", ValidDietRequest()),
                     await Client.PostAsync($"/api/trainer/trainees/not-a-guid/diet-plans/{Id<DietPlan>.New()}/activate", null),
                     await Client.PostAsync($"/api/trainer/trainees/not-a-guid/diet-plans/{Id<DietPlan>.New()}/delete", null),
                     await Client.GetAsync($"/api/trainer/trainees/not-a-guid/diet-plans/{Id<DietPlan>.New()}/history")
                 })
        {
            await AssertExactMessageAsync(response, HttpStatusCode.BadRequest, EnglishMessage(() => Messages.UserIdRequired));
        }

        foreach (var response in new[]
                 {
                     await Client.GetAsync($"/api/trainer/trainees/{validTraineeId}/diet-plans/not-a-guid"),
                     await Client.PostAsJsonAsync($"/api/trainer/trainees/{validTraineeId}/diet-plans/not-a-guid/update", ValidDietRequest()),
                     await Client.PostAsync($"/api/trainer/trainees/{validTraineeId}/diet-plans/not-a-guid/activate", null),
                     await Client.PostAsync($"/api/trainer/trainees/{validTraineeId}/diet-plans/not-a-guid/delete", null),
                     await Client.GetAsync($"/api/trainer/trainees/{validTraineeId}/diet-plans/not-a-guid/history")
                 })
        {
            await AssertExactMessageAsync(response, HttpStatusCode.BadRequest, EnglishMessage(() => Messages.FieldRequired));
        }
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/delete", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}", "trainer-shared", "active-relationship-allow")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/history", "trainer-shared", "active-relationship-allow")]
    public async Task CurrentTrainer_CanReadAndDeleteInheritedDietPlan()
    {
        var formerTrainer = await SeedTrainerAsync("trainer-diet-former", "trainer-diet-former@example.com");
        var currentTrainer = await SeedTrainerAsync("trainer-diet-current", "trainer-diet-current@example.com");
        var trainee = await SeedUserAsync("trainee-diet-owned", "trainee-diet-owned@example.com", "password123");
        await LinkTrainerAndTraineeAsync(formerTrainer.Id, trainee.Id);

        SetAuthorizationHeader(formerTrainer.Id);
        var plan = await CreateDietAsync(trainee.Id, "Owner plan", true);
        var originalPlan = await GetDietPlanSnapshotAsync(plan.Id);
        Assert.That((await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/unlink", null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await LinkTrainerAndTraineeAsync(currentTrainer.Id, trainee.Id);

        SetAuthorizationHeader(currentTrainer.Id);
        var singleRead = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}");
        Assert.That(singleRead.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var document = JsonDocument.Parse(await singleRead.Content.ReadAsStringAsync()))
        {
            Assert.That(document.RootElement.GetProperty("_id").GetString(), Is.EqualTo(plan.Id));
            Assert.That(document.RootElement.TryGetProperty("id", out _), Is.False);
            Assert.That(document.RootElement.GetProperty("name").GetString(), Is.EqualTo("Owner plan"));
        }

        Assert.That((await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/history")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        AssertDietPlanSnapshot((await GetDietPlanSnapshotAsync(plan.Id)), originalPlan);

        await AssertExactMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/delete", null),
            HttpStatusCode.OK,
            EnglishMessage(() => Messages.Deleted));
        await AssertExactMessageAsync(
            await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}"),
            HttpStatusCode.NotFound,
            EnglishMessage(() => Messages.DidntFind));

        var listAfterDelete = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans");
        Assert.That(listAfterDelete.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await listAfterDelete.Content.ReadAsStringAsync()), Is.EqualTo("[]"));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/history", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/update", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/activate", "trainer-shared", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/delete", "trainer-shared", "foreign-object-denial-no-mutation")]
    public async Task TrainerDietPlanRoutes_WithPlanFromDifferentTrainee_ReturnNotFoundWithoutMutation()
    {
        var trainer = await SeedTrainerAsync("trainer-diet-object-mismatch", "trainer-diet-object-mismatch@example.com");
        var planOwner = await SeedUserAsync("diet-object-owner", "diet-object-owner@example.com", "password123");
        var routeTrainee = await SeedUserAsync("diet-route-trainee", "diet-route-trainee@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, planOwner.Id);
        await LinkTrainerAndTraineeAsync(trainer.Id, routeTrainee.Id);

        SetAuthorizationHeader(trainer.Id);
        var plan = await CreateDietAsync(planOwner.Id, "Different trainee plan", false);
        var originalPlan = await GetDietPlanSnapshotAsync(plan.Id);
        var originalCounts = await GetDietPersistenceCountsAsync();
        var route = $"/api/trainer/trainees/{routeTrainee.Id}/diet-plans/{plan.Id}";

        await AssertExactMessageAsync(await Client.GetAsync(route), HttpStatusCode.NotFound, EnglishMessage(() => Messages.DidntFind));
        await AssertExactMessageAsync(await Client.GetAsync($"{route}/history"), HttpStatusCode.NotFound, EnglishMessage(() => Messages.DidntFind));
        await AssertExactMessageAsync(
            await Client.PostAsJsonAsync($"{route}/update", ValidDietRequest("Rejected route mismatch")),
            HttpStatusCode.NotFound,
            EnglishMessage(() => Messages.DidntFind));
        await AssertExactMessageAsync(await Client.PostAsync($"{route}/activate", null), HttpStatusCode.NotFound, EnglishMessage(() => Messages.DidntFind));
        await AssertExactMessageAsync(await Client.PostAsync($"{route}/delete", null), HttpStatusCode.NotFound, EnglishMessage(() => Messages.DidntFind));

        AssertDietPlanSnapshot((await GetDietPlanSnapshotAsync(plan.Id)), originalPlan);
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(originalCounts));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}", "trainer-shared", "anonymous-denial")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/history", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/history", "trainer-shared", "anonymous-denial")]
    public async Task TrainerDietSingleRead_WithoutRelationshipOrAuthentication_DoesNotMutatePlan()
    {
        var owner = await SeedTrainerAsync("task10-unrelated-diet-read-owner", "task10-unrelated-diet-read-owner@example.com");
        var unrelatedTrainer = await SeedTrainerAsync("task10-unrelated-diet-read-trainer", "task10-unrelated-diet-read-trainer@example.com");
        var trainee = await SeedUserAsync("task10-unrelated-diet-read-trainee", "task10-unrelated-diet-read-trainee@example.com", "password123");
        await LinkTrainerAndTraineeAsync(owner.Id, trainee.Id);

        SetAuthorizationHeader(owner.Id);
        var plan = await CreateDietAsync(trainee.Id, "Unrelated trainer read plan", true);
        var originalPlan = await GetDietPlanSnapshotAsync(plan.Id);
        var beforeRead = await GetDietPersistenceCountsAsync();

        SetAuthorizationHeader(unrelatedTrainer.Id);
        await AssertExactMessageAsync(
            await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}"),
            HttpStatusCode.NotFound,
            EnglishMessage(() => Messages.DidntFind));
        await AssertExactMessageAsync(
            await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/history"),
            HttpStatusCode.NotFound,
            EnglishMessage(() => Messages.DidntFind));
        AssertDietPlanSnapshot((await GetDietPlanSnapshotAsync(plan.Id)), originalPlan);
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(beforeRead));

        ClearAuthorizationHeader();
        using (var anonymousResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}"))
        {
            Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }
        using (var anonymousHistoryResponse = await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/history"))
        {
            Assert.That(anonymousHistoryResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        AssertDietPlanSnapshot((await GetDietPlanSnapshotAsync(plan.Id)), originalPlan);
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(beforeRead));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}", "trainer-shared", "former-relationship-denial")]
    [AuthorizationEvidence("GET", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/history", "trainer-shared", "former-relationship-denial")]
    public async Task TrainerDietSingleRead_AfterUnlink_ReturnsNotFoundAndPreservesPlan()
    {
        var trainer = await SeedTrainerAsync("task10-former-diet-read-trainer", "task10-former-diet-read-trainer@example.com");
        var trainee = await SeedUserAsync("task10-former-diet-read-trainee", "task10-former-diet-read-trainee@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        var plan = await CreateDietAsync(trainee.Id, "Former trainer read plan", true);
        var originalPlan = await GetDietPlanSnapshotAsync(plan.Id);
        using (var unlinkResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/unlink", null))
        {
            Assert.That(unlinkResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
        var beforeRead = await GetDietPersistenceCountsAsync();

        await AssertExactMessageAsync(
            await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}"),
            HttpStatusCode.NotFound,
            EnglishMessage(() => Messages.DidntFind));
        await AssertExactMessageAsync(
            await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/history"),
            HttpStatusCode.NotFound,
            EnglishMessage(() => Messages.DidntFind));

        AssertDietPlanSnapshot((await GetDietPlanSnapshotAsync(plan.Id)), originalPlan);
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(beforeRead));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainee/diet-plan/current", "own", "owner-allow")]
    [AuthorizationEvidence("GET", "/api/trainee/diet-plan/current", "own", "no-client-subject")]
    [AuthorizationEvidence("GET", "/api/trainee/diet-plans/current", "own", "owner-allow")]
    [AuthorizationEvidence("GET", "/api/trainee/diet-plans/current", "own", "no-client-subject")]
    public async Task TraineeCurrentDietRoutes_PreservePluralAndSingularSemantics()
    {
        var trainer = await SeedTrainerAsync("trainer-current-contract", "trainer-current-contract@example.com");
        var trainee = await SeedUserAsync("trainee-current-contract", "trainee-current-contract@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainee.Id);
        var emptyPlural = await Client.GetAsync("/api/trainee/diet-plans/current");
        Assert.That(emptyPlural.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await emptyPlural.Content.ReadAsStringAsync()), Is.EqualTo("[]"));
        await AssertExactMessageAsync(
            await Client.GetAsync("/api/trainee/diet-plan/current"),
            HttpStatusCode.NotFound,
            EnglishMessage(() => Messages.DidntFind));

        SetAuthorizationHeader(trainer.Id);
        var first = await CreateDietAsync(trainee.Id, "Training day", true);
        var second = await CreateDietAsync(trainee.Id, "Rest day", true);

        SetAuthorizationHeader(trainee.Id);
        var plural = await Client.GetAsync("/api/trainee/diet-plans/current");
        Assert.That(plural.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var plans = await plural.Content.ReadFromJsonAsync<List<DietPlanResponse>>();
        Assert.That(plans, Is.Not.Null);
        Assert.That(plans!.Select(plan => plan.Id), Is.SupersetOf(new[] { first.Id, second.Id }));
        Assert.That((plans).All(plan => plan.IsActive), Is.True);

        var singular = await Client.GetAsync("/api/trainee/diet-plan/current");
        Assert.That(singular.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var current = await singular.Content.ReadFromJsonAsync<DietPlanResponse>();
        Assert.That(current, Is.Not.Null);
        Assert.That(current!.Id, Is.AnyOf(first.Id, second.Id));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainee/diet-plan/current", "own", "anonymous-denial")]
    [AuthorizationEvidence("GET", "/api/trainee/diet-plans/current", "own", "anonymous-denial")]
    public async Task TraineeCurrentDietRoutes_WithoutAuthentication_AreUnauthorized()
    {
        ClearAuthorizationHeader();

        using var singularResponse = await Client.GetAsync("/api/trainee/diet-plan/current");
        using var pluralResponse = await Client.GetAsync("/api/trainee/diet-plans/current");

        Assert.That(singularResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(pluralResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainee/diet-plans", "own", "anonymous-denial")]
    [AuthorizationEvidence("GET", "/api/trainee/diet-plans/{dietPlanId}", "own", "anonymous-denial")]
    [AuthorizationEvidence("GET", "/api/trainee/diet-plans/{dietPlanId}/history", "own", "anonymous-denial")]
    [AuthorizationEvidence("POST", "/api/trainee/diet-plans/{dietPlanId}/update", "own", "anonymous-denial")]
    public async Task TraineeOwnDietRoutes_WithoutAuthentication_AreUnauthorized()
    {
        ClearAuthorizationHeader();
        var planId = Id<DietPlan>.New();

        using var listResponse = await Client.GetAsync("/api/trainee/diet-plans");
        using var detailResponse = await Client.GetAsync($"/api/trainee/diet-plans/{planId}");
        using var historyResponse = await Client.GetAsync($"/api/trainee/diet-plans/{planId}/history");
        using var updateResponse = await Client.PostAsJsonAsync(
            $"/api/trainee/diet-plans/{planId}/update",
            ValidDietRequest("Anonymous owner mutation"));

        Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(detailResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(historyResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/update", "trainer-shared", "active-relationship-allow")]
    public async Task TrainerDietUpdate_WithActiveRelationship_UpdatesPlan()
    {
        var trainer = await SeedTrainerAsync("task10-active-diet-trainer", "task10-active-diet-trainer@example.com");
        var trainee = await SeedUserAsync("task10-active-diet-trainee", "task10-active-diet-trainee@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        var plan = await CreateDietAsync(trainee.Id, "Active trainer plan", true);

        using var response = await Client.PostAsJsonAsync(
            $"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/update",
            ValidDietRequest("Active trainer updated plan"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updatedPlan = await response.Content.ReadFromJsonAsync<DietPlanResponse>();
        Assert.That(updatedPlan, Is.Not.Null);
        Assert.That(updatedPlan!.Name, Is.EqualTo("Active trainer updated plan"));

        var persistedPlan = await GetDietPlanSnapshotAsync(plan.Id);
        Assert.That(persistedPlan.Name, Is.EqualTo("Active trainer updated plan"));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/update", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/update", "trainer-shared", "anonymous-denial")]
    public async Task TrainerDietUpdate_WithoutRelationshipOrAuthentication_DoesNotMutatePlan()
    {
        var owner = await SeedTrainerAsync("task10-unrelated-diet-update-owner", "task10-unrelated-diet-update-owner@example.com");
        var unrelatedTrainer = await SeedTrainerAsync("task10-unrelated-diet-update-trainer", "task10-unrelated-diet-update-trainer@example.com");
        var trainee = await SeedUserAsync("task10-unrelated-diet-update-trainee", "task10-unrelated-diet-update-trainee@example.com", "password123");
        await LinkTrainerAndTraineeAsync(owner.Id, trainee.Id);

        SetAuthorizationHeader(owner.Id);
        var plan = await CreateDietAsync(trainee.Id, "Unrelated trainer update plan", true);
        var originalPlan = await GetDietPlanSnapshotAsync(plan.Id);
        var originalPersistenceCounts = await GetDietPersistenceCountsAsync();

        SetAuthorizationHeader(unrelatedTrainer.Id);
        await AssertExactMessageAsync(
            await Client.PostAsJsonAsync(
                $"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/update",
                ValidDietRequest("Unrelated trainer mutation")),
            HttpStatusCode.NotFound,
            EnglishMessage(() => Messages.DidntFind));
        AssertDietPlanSnapshot((await GetDietPlanSnapshotAsync(plan.Id)), originalPlan);
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(originalPersistenceCounts));

        ClearAuthorizationHeader();
        using (var anonymousResponse = await Client.PostAsJsonAsync(
                   $"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/update",
                   ValidDietRequest("Anonymous mutation")))
        {
            Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        AssertDietPlanSnapshot((await GetDietPlanSnapshotAsync(plan.Id)), originalPlan);
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(originalPersistenceCounts));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/update", "trainer-shared", "active-relationship-allow")]
    public async Task TrainerDietUpdate_CurrentTrainerCanUpdateInheritedPlanAndRecordsEditor()
    {
        var formerTrainer = await SeedTrainerAsync("task10-inherited-diet-update-author", "task10-inherited-diet-update-author@example.com");
        var currentTrainer = await SeedTrainerAsync("task10-inherited-diet-update-editor", "task10-inherited-diet-update-editor@example.com");
        var trainee = await SeedUserAsync("task10-foreign-diet-update-trainee", "task10-foreign-diet-update-trainee@example.com", "password123");
        await LinkTrainerAndTraineeAsync(formerTrainer.Id, trainee.Id);

        SetAuthorizationHeader(formerTrainer.Id);
        var plan = await CreateDietAsync(trainee.Id, "Foreign trainer update plan", true);
        Assert.That((await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/unlink", null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await LinkTrainerAndTraineeAsync(currentTrainer.Id, trainee.Id);
        var originalPersistenceCounts = await GetDietPersistenceCountsAsync();

        SetAuthorizationHeader(currentTrainer.Id);
        Assert.That((await Client.PostAsJsonAsync(
            $"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/update",
            ValidDietRequest("Foreign trainer mutation"))).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await GetDietPlanSnapshotAsync(plan.Id)).Name, Is.EqualTo("Foreign trainer mutation"));
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo((originalPersistenceCounts.Plans, originalPersistenceCounts.Histories + 1, originalPersistenceCounts.Commands + 1)));
        Assert.That((await GetDietPlanAuthorshipAsync(plan.Id)), Is.EqualTo((formerTrainer.Id, currentTrainer.Id)));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/update", "trainer-shared", "former-relationship-denial")]
    public async Task TrainerDietUpdate_AfterUnlink_ReturnsNotFoundAndPreservesPlan()
    {
        var trainer = await SeedTrainerAsync("task10-former-diet-trainer", "task10-former-diet-trainer@example.com");
        var trainee = await SeedUserAsync("task10-former-diet-trainee", "task10-former-diet-trainee@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        var plan = await CreateDietAsync(trainee.Id, "Former trainer plan", true);
        var originalPlan = await GetDietPlanSnapshotAsync(plan.Id);
        using (var unlinkResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/unlink", null))
        {
            Assert.That(unlinkResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
        var originalPersistenceCounts = await GetDietPersistenceCountsAsync();

        using var response = await Client.PostAsJsonAsync(
            $"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/update",
            ValidDietRequest("Former trainer mutation"));
        await AssertExactMessageAsync(response, HttpStatusCode.NotFound, EnglishMessage(() => Messages.DidntFind));

        AssertDietPlanSnapshot((await GetDietPlanSnapshotAsync(plan.Id)), originalPlan);
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(originalPersistenceCounts));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/activate", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/activate", "trainer-shared", "anonymous-denial")]
    public async Task TrainerDietActivate_WithoutRelationshipOrAuthentication_DoesNotMutatePlan()
    {
        var owner = await SeedTrainerAsync("task10-unrelated-diet-activate-owner", "task10-unrelated-diet-activate-owner@example.com");
        var unrelatedTrainer = await SeedTrainerAsync("task10-unrelated-diet-activate-trainer", "task10-unrelated-diet-activate-trainer@example.com");
        var trainee = await SeedUserAsync("task10-unrelated-diet-activate-trainee", "task10-unrelated-diet-activate-trainee@example.com", "password123");
        await LinkTrainerAndTraineeAsync(owner.Id, trainee.Id);

        SetAuthorizationHeader(owner.Id);
        var plan = await CreateDietAsync(trainee.Id, "Unrelated trainer activate plan", false);
        var originalPlan = await GetDietPlanSnapshotAsync(plan.Id);
        var originalPersistenceCounts = await GetDietPersistenceCountsAsync();

        SetAuthorizationHeader(unrelatedTrainer.Id);
        await AssertExactMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/activate", null),
            HttpStatusCode.NotFound,
            EnglishMessage(() => Messages.DidntFind));
        AssertDietPlanSnapshot((await GetDietPlanSnapshotAsync(plan.Id)), originalPlan);
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(originalPersistenceCounts));

        ClearAuthorizationHeader();
        using (var anonymousResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/activate", null))
        {
            Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        AssertDietPlanSnapshot((await GetDietPlanSnapshotAsync(plan.Id)), originalPlan);
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(originalPersistenceCounts));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/activate", "trainer-shared", "active-relationship-allow")]
    public async Task TrainerDietActivate_CurrentTrainerCanActivateInheritedPlan()
    {
        var formerTrainer = await SeedTrainerAsync("task10-inherited-diet-activate-author", "task10-inherited-diet-activate-author@example.com");
        var currentTrainer = await SeedTrainerAsync("task10-inherited-diet-activate-current", "task10-inherited-diet-activate-current@example.com");
        var trainee = await SeedUserAsync("task10-foreign-diet-activate-trainee", "task10-foreign-diet-activate-trainee@example.com", "password123");
        await LinkTrainerAndTraineeAsync(formerTrainer.Id, trainee.Id);

        SetAuthorizationHeader(formerTrainer.Id);
        var plan = await CreateDietAsync(trainee.Id, "Foreign trainer activate plan", false);
        Assert.That((await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/unlink", null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await LinkTrainerAndTraineeAsync(currentTrainer.Id, trainee.Id);
        var originalPersistenceCounts = await GetDietPersistenceCountsAsync();

        SetAuthorizationHeader(currentTrainer.Id);
        Assert.That((await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/activate", null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await GetDietPlanSnapshotAsync(plan.Id)).IsActive, Is.True);
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo((originalPersistenceCounts.Plans, originalPersistenceCounts.Histories + 1, originalPersistenceCounts.Commands + 1)));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/activate", "trainer-shared", "former-relationship-denial")]
    public async Task TrainerDietActivate_AfterUnlink_ReturnsNotFoundAndPreservesPlan()
    {
        var trainer = await SeedTrainerAsync("task10-former-diet-activate-trainer", "task10-former-diet-activate-trainer@example.com");
        var trainee = await SeedUserAsync("task10-former-diet-activate-trainee", "task10-former-diet-activate-trainee@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        var plan = await CreateDietAsync(trainee.Id, "Former trainer activate plan", false);
        var originalPlan = await GetDietPlanSnapshotAsync(plan.Id);
        using (var unlinkResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/unlink", null))
        {
            Assert.That(unlinkResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
        var originalPersistenceCounts = await GetDietPersistenceCountsAsync();

        await AssertExactMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/activate", null),
            HttpStatusCode.NotFound,
            EnglishMessage(() => Messages.DidntFind));

        AssertDietPlanSnapshot((await GetDietPlanSnapshotAsync(plan.Id)), originalPlan);
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(originalPersistenceCounts));
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/delete", "trainer-shared", "former-relationship-denial")]
    public async Task TrainerDietDelete_AfterUnlink_ReturnsNotFoundAndPreservesPlan()
    {
        var trainer = await SeedTrainerAsync("task10-former-diet-delete-trainer", "task10-former-diet-delete-trainer@example.com");
        var trainee = await SeedUserAsync("task10-former-diet-delete-trainee", "task10-former-diet-delete-trainee@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);

        SetAuthorizationHeader(trainer.Id);
        var plan = await CreateDietAsync(trainee.Id, "Former trainer delete plan", true);
        using (var unlinkResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/unlink", null))
        {
            Assert.That(unlinkResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        using var response = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/delete", null);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await response.Content.ReadAsStringAsync()), Does.Not.Contain("Former trainer delete plan"));

        using var verifyScope = Factory.Services.CreateScope();
        var database = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedPlan = await database.DietPlans.IgnoreQueryFilters().AsNoTracking().SingleAsync(item => item.Id.ToString() == plan.Id);
        Assert.That(persistedPlan.Name, Is.EqualTo("Former trainer delete plan"));
        Assert.That(persistedPlan.IsDeleted, Is.False);
    }

    [Test]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/delete", "trainer-shared", "unrelated-relationship-denial")]
    [AuthorizationEvidence("POST", "/api/trainer/trainees/{traineeId}/diet-plans/{dietPlanId}/delete", "trainer-shared", "anonymous-denial")]
    public async Task TrainerDietDelete_WithoutRelationshipOrAuthentication_DoesNotMutatePlan()
    {
        var owner = await SeedTrainerAsync("task10-unrelated-diet-owner", "task10-unrelated-diet-owner@example.com");
        var unrelatedTrainer = await SeedTrainerAsync("task10-unrelated-diet-trainer", "task10-unrelated-diet-trainer@example.com");
        var trainee = await SeedUserAsync("task10-unrelated-diet-trainee", "task10-unrelated-diet-trainee@example.com", "password123");
        await LinkTrainerAndTraineeAsync(owner.Id, trainee.Id);

        SetAuthorizationHeader(owner.Id);
        var plan = await CreateDietAsync(trainee.Id, "Unrelated trainer delete plan", true);

        SetAuthorizationHeader(unrelatedTrainer.Id);
        using (var unrelatedResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/delete", null))
        {
            Assert.That(unrelatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        ClearAuthorizationHeader();
        using (var anonymousResponse = await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/delete", null))
        {
            Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        using var verifyScope = Factory.Services.CreateScope();
        var database = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedPlan = await database.DietPlans.IgnoreQueryFilters().AsNoTracking().SingleAsync(item => item.Id.ToString() == plan.Id);
        Assert.That(persistedPlan.Name, Is.EqualTo("Unrelated trainer delete plan"));
        Assert.That(persistedPlan.IsDeleted, Is.False);
    }

    [Test]
    public async Task DietFailures_DoNotWriteOrQueueCommands()
    {
        var trainer = await SeedTrainerAsync("trainer-diet-failure", "trainer-diet-failure@example.com");
        var trainee = await SeedUserAsync("trainee-diet-failure", "trainee-diet-failure@example.com", "password123");
        var foreignTrainee = await SeedUserAsync("trainee-diet-foreign", "trainee-diet-foreign@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);
        SetAuthorizationHeader(trainer.Id);

        var beforeInvalidShape = await GetDietPersistenceCountsAsync();
        await AssertExactMessageAsync(
            await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans", new
            {
                name = " ",
                startDate = new DateOnly(2026, 7, 23),
                isActive = true,
                meals = new[] { new { name = "Meal", order = 0, estimatedCalories = 500 } }
            }),
            HttpStatusCode.BadRequest,
            EnglishMessage(() => Messages.FieldRequired));
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(beforeInvalidShape));

        await AssertExactMessageAsync(
            await Client.PostAsJsonAsync($"/api/trainer/trainees/{foreignTrainee.Id}/diet-plans", ValidDietRequest()),
            HttpStatusCode.NotFound,
            EnglishMessage(() => Messages.DidntFind));
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(beforeInvalidShape));

        var plan = await CreateDietAsync(trainee.Id, "Deleted failure plan", true);
        await AssertExactMessageAsync(
            await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/delete", null),
            HttpStatusCode.OK,
            EnglishMessage(() => Messages.Deleted));
        var afterDelete = await GetDietPersistenceCountsAsync();

        await AssertExactMessageAsync(
            await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/update", ValidDietRequest()),
            HttpStatusCode.NotFound,
            EnglishMessage(() => Messages.DidntFind));
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(afterDelete));
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

        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await createResponse.Content.ReadFromJsonAsync<DietPlanResponse>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Meals, Is.Empty);
        Assert.That(created.IsActive, Is.True);
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
        Assert.That(currentResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var currentPlans = await currentResponse.Content.ReadFromJsonAsync<List<DietPlanResponse>>();
        Assert.That(currentPlans, Is.Not.Null);
        Assert.That(currentPlans!.Select(x => x.Id), Is.SupersetOf(new[] { trainingDay.Id, restDay.Id }));
        Assert.That((currentPlans).All(x => x.IsActive), Is.True);
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

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationCommand = await db.CommandEnvelopes
            .Where(x => x.CommandTypeFullName.Contains("DietPlanUpdatedInAppNotificationCommand"))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        Assert.That(notificationCommand, Is.Not.Null);
        Assert.That(notificationCommand!.PayloadJson, Does.Contain("Diet Notification v2"));
        Assert.That(notificationCommand.PayloadJson, Does.Contain(trainee.Id.ToString()));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainee/diet-plans", "own", "owner-allow")]
    [AuthorizationEvidence("GET", "/api/trainee/diet-plans", "own", "no-client-subject")]
    [AuthorizationEvidence("GET", "/api/trainee/diet-plans/{dietPlanId}", "own", "owner-allow")]
    [AuthorizationEvidence("POST", "/api/trainee/diet-plans/{dietPlanId}/update", "own", "owner-allow")]
    [AuthorizationEvidence("GET", "/api/trainee/diet-plans/{dietPlanId}/history", "own", "owner-allow")]
    public async Task TraineeOwnDiet_AfterUnlink_RetainsInactiveHistoryAndCanUpdate()
    {
        var formerTrainer = await SeedTrainerAsync("former-diet-author", "former-diet-author@example.com");
        var trainee = await SeedUserAsync("diet-owner", "diet-owner@example.com", "password123");
        await LinkTrainerAndTraineeAsync(formerTrainer.Id, trainee.Id);
        SetAuthorizationHeader(formerTrainer.Id);
        var plan = await CreateDietAsync(trainee.Id, "Portable diet", false);
        Assert.That((await Client.PostAsync($"/api/trainer/trainees/{trainee.Id}/unlink", null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        SetAuthorizationHeader(trainee.Id);
        var list = await Client.GetAsync("/api/trainee/diet-plans");
        Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(((await list.Content.ReadFromJsonAsync<List<DietPlanResponse>>())!).Where(item => item.Id == plan.Id && !item.IsActive), Has.Exactly(1).Items);
        Assert.That((await Client.GetAsync($"/api/trainee/diet-plans/{plan.Id}")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await Client.GetAsync($"/api/trainee/diet-plans/{plan.Id}/history")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var update = await Client.PostAsJsonAsync($"/api/trainee/diet-plans/{plan.Id}/update", ValidDietRequest("Owner updated diet"));
        Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await update.Content.ReadFromJsonAsync<DietPlanResponse>())!.IsActive, Is.False);

        SetAuthorizationHeader(formerTrainer.Id);
        Assert.That((await Client.GetAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    [AuthorizationEvidence("GET", "/api/trainee/diet-plans/{dietPlanId}", "own", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("GET", "/api/trainee/diet-plans/{dietPlanId}/history", "own", "foreign-object-denial-no-mutation")]
    [AuthorizationEvidence("POST", "/api/trainee/diet-plans/{dietPlanId}/update", "own", "foreign-object-denial-no-mutation")]
    public async Task TraineeOwnDietRoutes_WithAnotherOwnersPlan_ReturnNotFoundWithoutMutation()
    {
        var trainer = await SeedTrainerAsync("foreign-owner-diet-author", "foreign-owner-diet-author@example.com");
        var owner = await SeedUserAsync("foreign-owner-diet-owner", "foreign-owner-diet-owner@example.com", "password123");
        var otherTrainee = await SeedUserAsync("foreign-owner-diet-other", "foreign-owner-diet-other@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, owner.Id);
        SetAuthorizationHeader(trainer.Id);
        var plan = await CreateDietAsync(owner.Id, "Foreign owner diet", false);
        var originalPlan = await GetDietPlanSnapshotAsync(plan.Id);
        var originalCounts = await GetDietPersistenceCountsAsync();

        SetAuthorizationHeader(otherTrainee.Id);
        var route = $"/api/trainee/diet-plans/{plan.Id}";
        await AssertExactMessageAsync(await Client.GetAsync(route), HttpStatusCode.NotFound, EnglishMessage(() => Messages.DidntFind));
        await AssertExactMessageAsync(await Client.GetAsync($"{route}/history"), HttpStatusCode.NotFound, EnglishMessage(() => Messages.DidntFind));
        await AssertExactMessageAsync(
            await Client.PostAsJsonAsync($"{route}/update", ValidDietRequest("Rejected foreign owner mutation")),
            HttpStatusCode.NotFound,
            EnglishMessage(() => Messages.DidntFind));

        AssertDietPlanSnapshot((await GetDietPlanSnapshotAsync(plan.Id)), originalPlan);
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(originalCounts));
    }

    [Test]
    public async Task UpdateOwnDietPlan_WithNullMeals_ReturnsLocalizedBadRequest()
    {
        var trainer = await SeedTrainerAsync("null-meals-diet-author", "null-meals-diet-author@example.com");
        var trainee = await SeedUserAsync("null-meals-diet-owner", "null-meals-diet-owner@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);
        SetAuthorizationHeader(trainer.Id);
        var plan = await CreateDietAsync(trainee.Id, "Null meals diet", false);
        SetAuthorizationHeader(trainee.Id);
        var request = new
        {
            name = "Null meals update",
            startDate = new DateOnly(2026, 7, 23),
            meals = (object?)null
        };

        SetRequestCulture("en");
        await AssertValidationMessageAsync(
            await Client.PostAsJsonAsync($"/api/trainee/diet-plans/{plan.Id}/update", request),
            GetMessageForCulture("en", () => Messages.FieldRequired));

        SetRequestCulture("pl");
        await AssertValidationMessageAsync(
            await Client.PostAsJsonAsync($"/api/trainee/diet-plans/{plan.Id}/update", request),
            GetMessageForCulture("pl", () => Messages.FieldRequired));
    }

    [Test]
    public async Task UpdateOwnDietPlan_TextBoundaries_AcceptMaximumsAndRejectMaxPlusOneBeforePersistence()
    {
        var trainer = await SeedTrainerAsync("diet-boundary-author", "diet-boundary-author@example.com");
        var trainee = await SeedUserAsync("diet-boundary-owner", "diet-boundary-owner@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);
        SetAuthorizationHeader(trainer.Id);
        var plan = await CreateDietAsync(trainee.Id, "Boundary diet", false);
        SetAuthorizationHeader(trainee.Id);
        var maximumName = new string('n', 120);
        var maximumNotes = new string('p', 2000);
        var maximumMealName = new string('m', 160);
        var maximumMealDescription = new string('d', 2000);
        var route = $"/api/trainee/diet-plans/{plan.Id}/update";

        using (var maximumResponse = await Client.PostAsJsonAsync(
                   route,
                   BoundaryDietRequest(maximumName, maximumNotes, maximumMealName, maximumMealDescription)))
        {
            Assert.That(maximumResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        var acceptedSnapshot = await GetDietPlanSnapshotAsync(plan.Id);
        Assert.That(acceptedSnapshot.Name, Has.Length.EqualTo(120));
        Assert.That(acceptedSnapshot.Notes, Has.Length.EqualTo(2000));
        Assert.That(acceptedSnapshot.Meals, Has.Exactly(1).Items);
        Assert.That(acceptedSnapshot.Meals[0].Name, Has.Length.EqualTo(160));
        Assert.That(acceptedSnapshot.Meals[0].Description, Has.Length.EqualTo(2000));
        var acceptedCounts = await GetDietPersistenceCountsAsync();
        var invalidRequests = new[]
        {
            (Field: "plan name", Request: BoundaryDietRequest(new string('n', 121), maximumNotes, maximumMealName, maximumMealDescription)),
            (Field: "plan notes", Request: BoundaryDietRequest(maximumName, new string('p', 2001), maximumMealName, maximumMealDescription)),
            (Field: "meal name", Request: BoundaryDietRequest(maximumName, maximumNotes, new string('m', 161), maximumMealDescription)),
            (Field: "meal description", Request: BoundaryDietRequest(maximumName, maximumNotes, maximumMealName, new string('d', 2001)))
        };

        foreach (var invalid in invalidRequests)
        {
            using var response = await Client.PostAsJsonAsync(route, invalid.Request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), $"{invalid.Field} exceeds its persisted maximum by one character");
            AssertDietPlanSnapshot((await GetDietPlanSnapshotAsync(plan.Id)), acceptedSnapshot);
            Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(acceptedCounts));
        }
    }

    [Test]
    public async Task TrainerDietPlanCreateAndUpdate_TextAbovePersistedMaximums_ReturnBadRequestWithoutPersistence()
    {
        var trainer = await SeedTrainerAsync("trainer-diet-boundary-author", "trainer-diet-boundary-author@example.com");
        var trainee = await SeedUserAsync("trainer-diet-boundary-owner", "trainer-diet-boundary-owner@example.com", "password123");
        await LinkTrainerAndTraineeAsync(trainer.Id, trainee.Id);
        SetAuthorizationHeader(trainer.Id);
        var plan = await CreateDietAsync(trainee.Id, "Trainer boundary diet", false);
        var originalSnapshot = await GetDietPlanSnapshotAsync(plan.Id);
        var originalCounts = await GetDietPersistenceCountsAsync();
        var maximumName = new string('n', 120);
        var maximumNotes = new string('p', 2000);
        var maximumMealName = new string('m', 160);
        var maximumMealDescription = new string('d', 2000);
        var invalidRequests = new[]
        {
            (Field: "plan name", Request: BoundaryDietRequest(new string('n', 121), maximumNotes, maximumMealName, maximumMealDescription)),
            (Field: "plan notes", Request: BoundaryDietRequest(maximumName, new string('p', 2001), maximumMealName, maximumMealDescription)),
            (Field: "meal name", Request: BoundaryDietRequest(maximumName, maximumNotes, new string('m', 161), maximumMealDescription)),
            (Field: "meal description", Request: BoundaryDietRequest(maximumName, maximumNotes, maximumMealName, new string('d', 2001)))
        };

        foreach (var invalid in invalidRequests)
        {
            using var createResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans", invalid.Request);
            Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), $"{invalid.Field} exceeds its persisted maximum by one character");

            using var updateResponse = await Client.PostAsJsonAsync($"/api/trainer/trainees/{trainee.Id}/diet-plans/{plan.Id}/update", invalid.Request);
            Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), $"{invalid.Field} exceeds its persisted maximum by one character");
        }

        AssertDietPlanSnapshot((await GetDietPlanSnapshotAsync(plan.Id)), originalSnapshot);
        Assert.That((await GetDietPersistenceCountsAsync()), Is.EqualTo(originalCounts));
    }

    [Test]
    public async Task TrainerRoleDietOwner_UsingTrainerUpdateRoute_PreservesIsActive()
    {
        var author = await SeedTrainerAsync("trainer-route-diet-author", "trainer-route-diet-author@example.com");
        var owner = await SeedTrainerAsync("trainer-route-diet-owner", "trainer-route-diet-owner@example.com");
        await LinkTrainerAndTraineeAsync(author.Id, owner.Id);
        SetAuthorizationHeader(author.Id);
        var plan = await CreateDietAsync(owner.Id, "Trainer owner diet", false);

        SetAuthorizationHeader(owner.Id);
        var response = await Client.PostAsJsonAsync(
            $"/api/trainer/trainees/{owner.Id}/diet-plans/{plan.Id}/update",
            ValidDietRequest("Trainer owner updated diet"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await response.Content.ReadFromJsonAsync<DietPlanResponse>())!.IsActive, Is.False);
        Assert.That((await GetDietPlanSnapshotAsync(plan.Id)).IsActive, Is.False);
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

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return (await response.Content.ReadFromJsonAsync<DietPlanResponse>())!;
    }

    private async Task<(int Plans, int Histories, int Commands)> GetDietPersistenceCountsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (
            await db.DietPlans.CountAsync(),
            await db.DietPlanHistories.CountAsync(),
            await db.CommandEnvelopes.CountAsync());
    }

    private async Task<DietPlanSnapshot> GetDietPlanSnapshotAsync(string dietPlanId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plan = await db.DietPlans
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(item => item.Meals)
            .SingleAsync(item => item.Id.ToString() == dietPlanId);

        return new DietPlanSnapshot(
            plan.Name,
            plan.StartDate,
            plan.EndDate,
            plan.EstimatedCalories,
            plan.ProteinGrams,
            plan.CarbsGrams,
            plan.FatGrams,
            plan.Notes,
            plan.IsActive,
            plan.IsDeleted,
            plan.Meals
                .OrderBy(meal => meal.Order)
                .Select(meal => new DietMealSnapshot(
                    meal.Name,
                    meal.Order,
                    meal.Description,
                    meal.EstimatedCalories,
                    meal.ProteinGrams,
                    meal.CarbsGrams,
                    meal.FatGrams))
                .ToArray());
    }

    private async Task<(Id<User> AuthorId, Id<User> EditorId)> GetDietPlanAuthorshipAsync(string dietPlanId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That(Id<DietPlan>.TryParse(dietPlanId, out var planId), Is.True);
        var authorId = await db.DietPlans
            .IgnoreQueryFilters()
            .Where(plan => plan.Id == planId)
            .Select(plan => plan.TrainerId)
            .SingleAsync();
        var editorId = await db.DietPlanHistories
            .Where(history => history.DietPlanId == planId && history.ChangeType == "Updated")
            .OrderByDescending(history => history.ChangeDate)
            .Select(history => history.ChangedByUserId)
            .FirstAsync();
        return (authorId, editorId);
    }

    private static object ValidDietRequest(string name = "Valid diet")
        => new
        {
            name,
            startDate = new DateOnly(2026, 7, 23),
            isActive = true,
            meals = new[] { new { name = "Meal", order = 0, estimatedCalories = 500 } }
        };

    private static object BoundaryDietRequest(string name, string notes, string mealName, string mealDescription)
        => new
        {
            name,
            startDate = new DateOnly(2026, 7, 23),
            notes,
            isActive = true,
            meals = new[] { new { name = mealName, order = 0, description = mealDescription, estimatedCalories = 500 } }
        };

    private static async Task AssertExactMessageAsync(HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedMessage)
    {
        Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
        Assert.That((await response.Content.ReadAsStringAsync()), Is.EqualTo(JsonSerializer.Serialize(new { msg = expectedMessage })));
    }

    private static async Task AssertValidationMessageAsync(HttpResponseMessage response, string expectedMessage)
    {
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = body.RootElement.GetProperty("errors").GetProperty("meals");
        Assert.That(errors.GetArrayLength(), Is.EqualTo(1));
        Assert.That(errors[0].GetString(), Is.EqualTo(expectedMessage));
    }

    private void SetRequestCulture(string culture)
    {
        Client.DefaultRequestHeaders.Remove("Accept-Language");
        Assert.That(Client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", culture), Is.True);
    }

    private static string EnglishMessage(Func<string> getMessage) => GetMessageForCulture("en", getMessage);

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

    private static void AssertDietPlanSnapshot(DietPlanSnapshot actual, DietPlanSnapshot expected)
    {
        using var assertions = Assert.EnterMultipleScope();
        Assert.That(actual, Is.EqualTo(expected).UsingPropertiesComparer(
            options => options.Excluding(nameof(DietPlanSnapshot.Meals))));
        Assert.That(actual.Meals, Is.EquivalentTo(expected.Meals));
    }

    private sealed record DietPlanSnapshot(
        string Name,
        DateOnly StartDate,
        DateOnly? EndDate,
        int? EstimatedCalories,
        decimal? ProteinGrams,
        decimal? CarbsGrams,
        decimal? FatGrams,
        string? Notes,
        bool IsActive,
        bool IsDeleted,
        IReadOnlyList<DietMealSnapshot> Meals);

    private sealed record DietMealSnapshot(
        string Name,
        int Order,
        string? Description,
        int? EstimatedCalories,
        decimal? ProteinGrams,
        decimal? CarbsGrams,
        decimal? FatGrams);
}
