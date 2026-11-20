using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class ExerciseTests : IntegrationTestBase
{
    [TestCase("api/exercise/addExercise")]
    [TestCase("api/exercise/addExerciseWithFormula")]
    [TestCase("api/exercise/{id}/addGlobalTranslation")]
    public void GlobalExerciseWriteRoutes_RequireManageGlobalExercisesPolicy(string route)
    {
        var endpoint = Factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(candidate =>
                string.Equals(candidate.RoutePattern.RawText, route, StringComparison.Ordinal)
                && candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("POST", StringComparer.Ordinal) == true);

        Assert.That(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Select(metadata => metadata.Policy), Is.EqualTo(new[] { AuthConstants.Policies.ManageGlobalExercises }));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/addExercise", "admin", "current-permission-allow")]
    public async Task AddExercise_WithValidData_CreatesGlobalExercise()
    {
        var manager = await SeedAdminAsync();
        SetAuthorizationHeader(manager.Id);

        var request = new
        {
            name = "Bench Press",
            bodyPart = BodyParts.Chest.ToString(),
            description = "Classic chest exercise"
        };

        var response = await Client.PostAsJsonAsync("/api/exercise/addExercise", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("Created"));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exercise = await db.Exercises.FirstOrDefaultAsync(e => e.Name == "Bench Press" && e.UserId == null);
        Assert.That(exercise, Is.Not.Null);
        Assert.That(exercise!.BodyPart.ToString(), Is.EqualTo("Chest"));
        Assert.That(exercise.EloFormula, Is.EqualTo(ExerciseEloFormula.Standard));
        Assert.That(exercise.IsDeleted, Is.False);
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/addExerciseWithFormula", "admin", "current-permission-allow")]
    public async Task AddExerciseWithFormula_AsAuthorizedUser_PersistsFormula()
    {
        var user = await SeedAdminAsync();
        SetAuthorizationHeader(user.Id);

        var request = new
        {
            name = "Weighted Pullup",
            bodyPart = BodyParts.Back.ToString(),
            eloFormula = ExerciseEloFormula.StrengthWeighted.ToString(),
            description = "Weighted pullup"
        };

        var response = await PostAsJsonWithApiOptionsAsync("/api/exercise/addExerciseWithFormula", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exercise = await db.Exercises.FirstOrDefaultAsync(e => e.Name == "Weighted Pullup" && e.UserId == null);
        Assert.That(exercise, Is.Not.Null);
        Assert.That(exercise!.EloFormula, Is.EqualTo(ExerciseEloFormula.StrengthWeighted));
    }

    [TestCase("/api/exercise/addExercise", false)]
    [TestCase("/api/exercise/addExerciseWithFormula", true)]
    public async Task GlobalExerciseCreate_AsCurrentNonManager_ReturnsForbiddenWithoutCreatingExercise(
        string route,
        bool includeFormula)
    {
        var attacker = await SeedUserAsync(
            name: $"global-create-attacker-{includeFormula}",
            email: $"global-create-attacker-{includeFormula}@example.com");
        SetAuthorizationHeader(attacker.Id);
        var attemptedExerciseName = $"Denied Global Exercise {includeFormula}";
        object request = includeFormula
            ? new
            {
                name = attemptedExerciseName,
                bodyPart = BodyParts.Back.ToString(),
                eloFormula = ExerciseEloFormula.StrengthWeighted.ToString(),
                description = "Must not be persisted"
            }
            : new
            {
                name = attemptedExerciseName,
                bodyPart = BodyParts.Back.ToString(),
                description = "Must not be persisted"
            };

        var response = await PostAsJsonWithApiOptionsAsync(route, request);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var exerciseExists = await db.Exercises
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(exercise => exercise.Name == attemptedExerciseName);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(exerciseExists, Is.False);
        }
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/{id}/addUserExerciseWithFormula", "admin", "current-permission-allow")]
    public async Task AddUserExerciseWithFormula_AsAuthorizedUser_PersistsFormula()
    {
        var user = await SeedAdminAsync();
        SetAuthorizationHeader(user.Id);

        var request = new
        {
            name = "Weighted Pullup User",
            bodyPart = BodyParts.Back.ToString(),
            eloFormula = ExerciseEloFormula.VolumeWeighted.ToString(),
            description = "Weighted pullup user"
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/exercise/{user.Id}/addUserExerciseWithFormula", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exercise = await db.Exercises.FirstOrDefaultAsync(e => e.Name == "Weighted Pullup User" && e.UserId == user.Id);
        Assert.That(exercise, Is.Not.Null);
        Assert.That(exercise!.EloFormula, Is.EqualTo(ExerciseEloFormula.VolumeWeighted));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/{id}/addUserExercise", "own", "owner-allow")]
    public async Task AddUserExercise_WithValidData_CreatesUserExercise()
    {
        var user = await SeedUserAsync(name: "exerciseuser", email: "exercise@example.com");
        SetAuthorizationHeader(user.Id);

        var request = new
        {
            name = "Custom Squat",
            bodyPart = BodyParts.Quads.ToString(),
            description = "Custom leg exercise"
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/exercise/{user.Id}/addUserExercise", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exercise = await db.Exercises.FirstOrDefaultAsync(e => e.Name == "Custom Squat" && e.UserId == user.Id);
        Assert.That(exercise, Is.Not.Null);
        Assert.That(exercise!.BodyPart.ToString(), Is.EqualTo("Quads"));
    }

    [Test]
    public async Task AddExercise_WithoutName_ReturnsBadRequest()
    {
        var user = await SeedAdminAsync();
        SetAuthorizationHeader(user.Id);

        var request = new
        {
            name = "",
            bodyPart = BodyParts.Chest.ToString()
        };

        var response = await Client.PostAsJsonAsync("/api/exercise/addExercise", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task AddExercise_WithInvalidBodyPart_ReturnsBadRequest()
    {
        var user = await SeedAdminAsync();
        SetAuthorizationHeader(user.Id);

        var request = new
        {
            name = "Test Exercise",
            bodyPart = "InvalidBodyPart"
        };

        var response = await Client.PostAsJsonAsync("/api/exercise/addExercise", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/{id}/deleteExercise", "own", "owner-allow")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/{id}/deleteExercise", "own", "owner-custom-allow")]
    public async Task DeleteExercise_AsOwner_MarksAsDeleted()
    {
        var user = await SeedUserAsync(name: "exerciseuser", email: "exercise@example.com");
        var exercise = await SeedExerciseAsync(user.Id, "Test Exercise", "Chest");
        SetAuthorizationHeader(user.Id);

        var request = new Dictionary<string, string>
        {
            { "id", exercise.Id.ToString() }
        };

        var response = await Client.PostAsJsonAsync($"/api/exercise/{user.Id}/deleteExercise", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var deletedExercise = await db.Exercises
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == exercise.Id);
        Assert.That(deletedExercise, Is.Not.Null);
        Assert.That(deletedExercise!.IsDeleted, Is.True);
    }

    [Test]
    public async Task DeleteExercise_AsAdmin_CanDeleteGlobalExercise()
    {
        var admin = await SeedUserAsync(name: "admin", email: "admin@example.com", isAdmin: true);
        var exercise = await SeedExerciseAsync(null, "Global Exercise", "Chest");
        SetAuthorizationHeader(admin.Id);

        var request = new Dictionary<string, string>
        {
            { "id", exercise.Id.ToString() }
        };

        var response = await Client.PostAsJsonAsync($"/api/exercise/{admin.Id}/deleteExercise", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var deletedExercise = await db.Exercises
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == exercise.Id);
        Assert.That(deletedExercise!.IsDeleted, Is.True);
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/{id}/deleteExercise", "manager-own", "owner-allow")]
    public async Task DeleteExercise_AsManager_DeletesOwnCustomExercise()
    {
        var manager = await SeedAdminAsync();
        var exercise = await SeedExerciseAsync(manager.Id, "Manager Custom Delete", "Chest");
        SetAuthorizationHeader(manager.Id);

        var response = await Client.PostAsJsonAsync(
            $"/api/exercise/{manager.Id}/deleteExercise",
            new Dictionary<string, string> { ["id"] = exercise.Id.ToString() });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That((await db.Exercises.IgnoreQueryFilters().SingleAsync(item => item.Id == exercise.Id)).IsDeleted, Is.True);
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/{id}/deleteExercise", "own", "foreign-custom-denial")]
    public async Task DeleteExercise_AsManager_CannotDeleteForeignCustomExercise()
    {
        var manager = await SeedAdminAsync();
        var victim = await SeedUserAsync(name: "manager-delete-victim", email: "manager-delete-victim@example.com");
        var exercise = await SeedExerciseAsync(victim.Id, "Manager Denied Private Delete", "Chest");
        SetAuthorizationHeader(manager.Id);

        var response = await Client.PostAsJsonAsync(
            $"/api/exercise/{manager.Id}/deleteExercise",
            new Dictionary<string, string> { ["id"] = exercise.Id.ToString() });

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedExercise = await db.Exercises
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == exercise.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(persistedExercise.UserId, Is.EqualTo(victim.Id));
            Assert.That(persistedExercise.IsDeleted, Is.False);
        }
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/{id}/deleteExercise", "own", "foreign-object-denial-no-mutation")]
    public async Task DeleteExercise_NonOwnerNonAdmin_ReturnsNotFoundWithoutMutation()
    {
        var (attacker, victim, victimExercise) = await SeedForeignExerciseScenarioAsync(
            "foreign-delete-own-route",
            "Victim Private Delete By Object",
            BodyParts.Back);
        SetAuthorizationHeader(attacker.Id);

        var request = new Dictionary<string, string>
        {
            { "id", victimExercise.Id.ToString() }
        };

        var response = await Client.PostAsJsonAsync($"/api/exercise/{attacker.Id}/deleteExercise", request);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedExercise = await db.Exercises
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == victimExercise.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(responseBody, Does.Not.Contain(victimExercise.Name));
            Assert.That(responseBody, Does.Not.Contain(victimExercise.Id.ToString()));
            Assert.That(persistedExercise.UserId, Is.EqualTo(victim.Id));
            Assert.That(persistedExercise.Name, Is.EqualTo(victimExercise.Name));
            Assert.That(persistedExercise.IsDeleted, Is.False);
        }
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/updateExercise", "own", "owner-allow")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/updateExercise", "own", "owner-custom-allow")]
    public async Task UpdateExercise_WithValidData_UpdatesExercise()
    {
        var user = await SeedUserAsync(name: "exerciseuser", email: "exercise@example.com");
        var exercise = await SeedExerciseAsync(user.Id, "Test Exercise", "Chest");
        SetAuthorizationHeader(user.Id);

        var request = new
        {
            _id = exercise.Id.ToString(),
            name = "New Name",
            bodyPart = BodyParts.Back.ToString(),
            description = "Updated description"
        };

        var response = await PostAsJsonWithApiOptionsAsync("/api/exercise/updateExercise", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var updatedExercise = await db.Exercises.FirstOrDefaultAsync(e => e.Id == exercise.Id);
        Assert.That(updatedExercise, Is.Not.Null);
        Assert.That(updatedExercise!.Name, Is.EqualTo("New Name"));
        Assert.That(updatedExercise.BodyPart.ToString(), Is.EqualTo("Back"));
        Assert.That(updatedExercise.Description, Is.EqualTo("Updated description"));
        Assert.That(updatedExercise.EloFormula, Is.EqualTo(ExerciseEloFormula.Standard));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/updateExercise", "manager-own", "owner-allow")]
    public async Task UpdateExercise_AsManager_UpdatesOwnCustomExercise()
    {
        var manager = await SeedAdminAsync();
        var exercise = await SeedExerciseAsync(manager.Id, "Manager Custom Update", "Chest");
        SetAuthorizationHeader(manager.Id);

        var response = await PostAsJsonWithApiOptionsAsync("/api/exercise/updateExercise", new
        {
            _id = exercise.Id.ToString(),
            name = "Manager Custom Updated",
            bodyPart = BodyParts.Back.ToString(),
            description = "Owned by manager"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updatedExercise = await db.Exercises.SingleAsync(item => item.Id == exercise.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(updatedExercise.UserId, Is.EqualTo(manager.Id));
            Assert.That(updatedExercise.Name, Is.EqualTo("Manager Custom Updated"));
            Assert.That(updatedExercise.BodyPart, Is.EqualTo(BodyParts.Back));
        }
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/updateExerciseWithFormula", "admin", "current-permission-allow")]
    public async Task UpdateExerciseWithFormula_AsAuthorizedUser_UpdatesFormula()
    {
        var user = await SeedAdminAsync();
        var exercise = await SeedExerciseAsync(null, "Update Formula", "Chest");
        SetAuthorizationHeader(user.Id);

        var request = new
        {
            _id = exercise.Id.ToString(),
            name = "Update Formula",
            bodyPart = BodyParts.Back.ToString(),
            eloFormula = ExerciseEloFormula.StrengthWeighted.ToString(),
            description = "Updated with formula"
        };

        var response = await PostAsJsonWithApiOptionsAsync("/api/exercise/updateExerciseWithFormula", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var updatedExercise = await db.Exercises.FirstOrDefaultAsync(e => e.Id == exercise.Id);
        Assert.That(updatedExercise, Is.Not.Null);
        Assert.That(updatedExercise!.EloFormula, Is.EqualTo(ExerciseEloFormula.StrengthWeighted));
        Assert.That(updatedExercise.BodyPart.ToString(), Is.EqualTo("Back"));
        Assert.That(updatedExercise.Description, Is.EqualTo("Updated with formula"));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/updateExercise", "own", "foreign-custom-denial")]
    public async Task UpdateExercise_WhenUserIsNotOwnerAndHasNoPermission_ReturnsNotFoundWithoutMutation()
    {
        var (attacker, victim, victimExercise) = await SeedForeignExerciseScenarioAsync(
            "foreign-update",
            "Victim Private Update",
            BodyParts.Chest);
        SetAuthorizationHeader(attacker.Id);

        var request = new
        {
            _id = victimExercise.Id.ToString(),
            name = "Attacker Update Attempt",
            bodyPart = BodyParts.Back.ToString(),
            description = "Must not be persisted"
        };

        var response = await PostAsJsonWithApiOptionsAsync("/api/exercise/updateExercise", request);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedExercise = await db.Exercises
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == victimExercise.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(responseBody, Does.Not.Contain(victimExercise.Name));
            Assert.That(responseBody, Does.Not.Contain(victimExercise.Id.ToString()));
            Assert.That(persistedExercise.UserId, Is.EqualTo(victim.Id));
            Assert.That(persistedExercise.Name, Is.EqualTo(victimExercise.Name));
            Assert.That(persistedExercise.BodyPart, Is.EqualTo(BodyParts.Chest));
            Assert.That(persistedExercise.Description, Is.Null);
            Assert.That(persistedExercise.IsDeleted, Is.False);
        }
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/exercise/getAllGlobalExercises", "authenticated-global", "ordinary-authenticated-allow")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/exercise/getAllGlobalExercises", "authenticated-global", "global-resource-allow")]
    public async Task GetAllGlobalExercises_ReturnsOnlyGlobalExercises()
    {
        var user = await SeedUserAsync(name: "exerciseuser", email: "exercise@example.com");
        await SeedExerciseAsync(null, "Global Exercise 1", "Chest");
        await SeedExerciseAsync(null, "Global Exercise 2", "Back");
        await SeedExerciseAsync(user.Id, "User Exercise", "Chest");
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync("/api/exercise/getAllGlobalExercises");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<List<ExerciseResponse>>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body, Has.Exactly(2).Items);
        Assert.That(body!.Select(e => e.Name), Is.SupersetOf(new[] { "Global Exercise 1", "Global Exercise 2" }));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/exercise/{id}/getAllUserExercises", "own", "owner-allow")]
    public async Task GetAllUserExercises_ReturnsOnlyUserExercises()
    {
        var user = await SeedUserAsync(name: "exerciseuser", email: "exercise@example.com");
        await SeedExerciseAsync(null, "Global Exercise", "Chest");
        await SeedExerciseAsync(user.Id, "User Exercise 1", "Chest");
        await SeedExerciseAsync(user.Id, "User Exercise 2", "Back");
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync($"/api/exercise/{user.Id}/getAllUserExercises");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<List<ExerciseResponse>>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body, Has.Exactly(2).Items);
        Assert.That(body!.Select(e => e.Name), Is.SupersetOf(new[] { "User Exercise 1", "User Exercise 2" }));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/exercise/{id}/getAllExercises", "own", "owner-allow")]
    public async Task GetAllExercises_ReturnsGlobalAndUserExercises()
    {
        var user = await SeedUserAsync(name: "exerciseuser", email: "exercise@example.com");
        await SeedExerciseAsync(null, "Global Exercise", "Chest");
        await SeedExerciseAsync(user.Id, "User Exercise", "Back");
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync($"/api/exercise/{user.Id}/getAllExercises");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<List<ExerciseResponse>>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body, Has.Exactly(2).Items);
        Assert.That(body!.Select(e => e.Name), Is.SupersetOf(new[] { "Global Exercise", "User Exercise" }));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/{id}/getExerciseByBodyPart", "own", "owner-allow")]
    public async Task GetExerciseByBodyPart_FiltersCorrectly()
    {
        var user = await SeedUserAsync(name: "exerciseuser", email: "exercise@example.com");
        await SeedExerciseAsync(null, "Chest Exercise", "Chest");
        await SeedExerciseAsync(null, "Back Exercise", "Back");
        SetAuthorizationHeader(user.Id);

        var request = new { bodyPart = BodyParts.Chest.ToString() };

        var response = await Client.PostAsJsonAsync($"/api/exercise/{user.Id}/getExerciseByBodyPart", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<List<ExerciseResponse>>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body, Has.Exactly(1).Items);
        Assert.That(body![0].Name, Is.EqualTo("Chest Exercise"));
    }

    [Test]
    public async Task GetExerciseByBodyPart_WithInvalidBodyPart_ReturnsBadRequest()
    {
        var user = await SeedUserAsync(name: "exerciseuseralias", email: "exercisealias@example.com");
        SetAuthorizationHeader(user.Id);

        var request = new { bodyPart = "ChestAlias" };
        var response = await Client.PostAsJsonAsync($"/api/exercise/{user.Id}/getExerciseByBodyPart", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetExerciseByBodyPart_WithNumericBodyPart_ReturnsBadRequest()
    {
        var user = await SeedUserAsync(name: "exerciseusernum", email: "exercisenum@example.com");
        SetAuthorizationHeader(user.Id);

        var request = new { bodyPart = 1 };
        var response = await Client.PostAsJsonAsync($"/api/exercise/{user.Id}/getExerciseByBodyPart", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/exercise/{id}/getExercise", "authenticated-global", "ordinary-authenticated-allow")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/exercise/{id}/getExercise", "authenticated-global", "owner-custom-allow")]
    public async Task GetExercise_WithValidId_ReturnsExercise()
    {
        var user = await SeedUserAsync(name: "exerciseuser", email: "exercise@example.com");
        var exercise = await SeedExerciseAsync(user.Id, "Test Exercise", "Chest", eloFormula: ExerciseEloFormula.StrengthWeighted);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seededExercise = await db.Exercises.FirstAsync(e => e.Id == exercise.Id);
            seededExercise.Description = "Detailed description";
            seededExercise.Image = "https://cdn.example.com/exercise.png";
            await db.SaveChangesAsync();
        }

        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync($"/api/exercise/{exercise.Id}/getExercise");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<ExerciseResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Name, Is.EqualTo("Test Exercise"));
        Assert.That(body.BodyPart, Is.Not.Null);
        Assert.That(body.BodyPart!.Name, Is.EqualTo("Chest"));
        Assert.That(body.Description, Is.EqualTo("Detailed description"));
        Assert.That(body.Image, Is.EqualTo("https://cdn.example.com/exercise.png"));
        Assert.That(body.EloFormula, Is.Not.Null);
        Assert.That(body.EloFormula!.Id, Is.EqualTo(ExerciseEloFormula.StrengthWeighted.ToString()));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/exercise/{id}/getExercise", "authenticated-global", "foreign-custom-denial")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/exercise/{id}/getExercise", "authenticated-global", "ordinary-manager-denial")]
    public async Task GetExercise_AsDifferentUser_ReturnsNotFoundWithoutDisclosingCustomExercise()
    {
        var (attacker, _, victimExercise) = await SeedForeignExerciseScenarioAsync(
            "foreign-detail",
            "Victim Private Detail",
            BodyParts.Chest);
        SetAuthorizationHeader(attacker.Id);

        var response = await Client.GetAsync($"/api/exercise/{victimExercise.Id}/getExercise");
        var responseBody = await response.Content.ReadAsStringAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedExercise = await db.Exercises
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(exercise => exercise.Id == victimExercise.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(responseBody, Does.Not.Contain(victimExercise.Name));
            Assert.That(responseBody, Does.Not.Contain(victimExercise.Id.ToString()));
            Assert.That(persistedExercise.UserId, Is.EqualTo(victimExercise.UserId));
            Assert.That(persistedExercise.Name, Is.EqualTo(victimExercise.Name));
            Assert.That(persistedExercise.IsDeleted, Is.False);
        }
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/exercise/{id}/getExercise", "manager", "foreign-custom-denial")]
    public async Task GetExercise_AsManager_ReturnsNotFoundWithoutDisclosingForeignCustomExercise()
    {
        var manager = await SeedAdminAsync();
        var victim = await SeedUserAsync(name: "manager-detail-victim", email: "manager-detail-victim@example.com");
        var exercise = await SeedExerciseAsync(victim.Id, "Manager Denied Private Detail", "Back");
        SetAuthorizationHeader(manager.Id);

        var response = await Client.GetAsync($"/api/exercise/{exercise.Id}/getExercise");
        var responseBody = await response.Content.ReadAsStringAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedExercise = await db.Exercises
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == exercise.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(responseBody, Does.Not.Contain(exercise.Name));
            Assert.That(responseBody, Does.Not.Contain(exercise.Id.ToString()));
            Assert.That(persistedExercise.UserId, Is.EqualTo(victim.Id));
            Assert.That(persistedExercise.IsDeleted, Is.False);
        }
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/{id}/addUserExercise", "own", "foreign-object-denial-no-mutation")]
    public async Task AddUserExercise_WithVictimRoute_ReturnsForbiddenWithoutCreatingExercise()
    {
        var (attacker, victim, victimExercise) = await SeedForeignExerciseScenarioAsync(
            "foreign-create",
            "Victim Existing Create",
            BodyParts.Quads);
        SetAuthorizationHeader(attacker.Id);
        const string attemptedExerciseName = "Attacker Assigned To Victim";
        var request = new
        {
            name = attemptedExerciseName,
            bodyPart = BodyParts.Quads.ToString(),
            description = "Must not be persisted"
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/exercise/{victim.Id}/addUserExercise", request);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var attemptedExercise = await db.Exercises
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(exercise => exercise.Name == attemptedExerciseName);
        var attemptedExerciseExists = attemptedExercise is not null;
        var persistedVictimExercise = await db.Exercises
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(exercise => exercise.Id == victimExercise.Id);

        if (attemptedExercise is not null)
        {
            Assert.That(attemptedExercise.UserId, Is.EqualTo(victim.Id));
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(responseBody, Does.Not.Contain(victimExercise.Name));
            Assert.That(attemptedExerciseExists, Is.False, string.Format(System.Globalization.CultureInfo.InvariantCulture, "an attacker must not create exercise '{0}' for victim {1}; the persisted owner was {2}", attemptedExerciseName, victim.Id, attemptedExercise?.UserId));
            Assert.That(persistedVictimExercise.UserId, Is.EqualTo(victim.Id));
            Assert.That(persistedVictimExercise.IsDeleted, Is.False);
        }
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/exercise/{id}/getAllUserExercises", "own", "foreign-object-denial-no-mutation")]
    public async Task GetAllUserExercises_WithVictimRoute_ReturnsForbiddenWithoutDisclosingExercises()
    {
        var (attacker, victim, victimExercise) = await SeedForeignExerciseScenarioAsync(
            "foreign-list",
            "Victim Private List",
            BodyParts.Back);
        SetAuthorizationHeader(attacker.Id);

        var response = await Client.GetAsync($"/api/exercise/{victim.Id}/getAllUserExercises");
        var responseBody = await response.Content.ReadAsStringAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedExercise = await db.Exercises
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(exercise => exercise.Id == victimExercise.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(responseBody, Does.Not.Contain(victimExercise.Name));
            Assert.That(responseBody, Does.Not.Contain(victimExercise.Id.ToString()));
            Assert.That(persistedExercise.UserId, Is.EqualTo(victim.Id));
            Assert.That(persistedExercise.Name, Is.EqualTo(victimExercise.Name));
            Assert.That(persistedExercise.IsDeleted, Is.False);
        }
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/exercise/{id}/getAllExercises", "own", "foreign-object-denial-no-mutation")]
    public async Task GetAllExercises_WithVictimRoute_ReturnsForbiddenWithoutDisclosingExercises()
    {
        var (attacker, victim, victimExercise) = await SeedForeignExerciseScenarioAsync(
            "foreign-combined-list",
            "Victim Private Combined List",
            BodyParts.Biceps);
        SetAuthorizationHeader(attacker.Id);

        var response = await Client.GetAsync($"/api/exercise/{victim.Id}/getAllExercises");
        var responseBody = await response.Content.ReadAsStringAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedExercise = await db.Exercises
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(exercise => exercise.Id == victimExercise.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(responseBody, Does.Not.Contain(victimExercise.Name));
            Assert.That(responseBody, Does.Not.Contain(victimExercise.Id.ToString()));
            Assert.That(persistedExercise.UserId, Is.EqualTo(victim.Id));
            Assert.That(persistedExercise.Name, Is.EqualTo(victimExercise.Name));
            Assert.That(persistedExercise.IsDeleted, Is.False);
        }
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/{id}/getExerciseByBodyPart", "own", "foreign-object-denial-no-mutation")]
    public async Task GetExerciseByBodyPart_WithVictimRoute_ReturnsForbiddenWithoutDisclosingExercises()
    {
        var (attacker, victim, victimExercise) = await SeedForeignExerciseScenarioAsync(
            "foreign-body-part",
            "Victim Private Body Part",
            BodyParts.Shoulders);
        SetAuthorizationHeader(attacker.Id);
        var request = new { bodyPart = BodyParts.Shoulders.ToString() };

        var response = await Client.PostAsJsonAsync($"/api/exercise/{victim.Id}/getExerciseByBodyPart", request);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedExercise = await db.Exercises
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(exercise => exercise.Id == victimExercise.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(responseBody, Does.Not.Contain(victimExercise.Name));
            Assert.That(responseBody, Does.Not.Contain(victimExercise.Id.ToString()));
            Assert.That(persistedExercise.UserId, Is.EqualTo(victim.Id));
            Assert.That(persistedExercise.Name, Is.EqualTo(victimExercise.Name));
            Assert.That(persistedExercise.IsDeleted, Is.False);
        }
    }

    [Test]
    public async Task DeleteExercise_WithVictimRouteAndExercise_ReturnsForbiddenWithoutDeletingExercise()
    {
        var (attacker, victim, victimExercise) = await SeedForeignExerciseScenarioAsync(
            "foreign-delete",
            "Victim Private Delete",
            BodyParts.Triceps);
        SetAuthorizationHeader(attacker.Id);
        var request = new Dictionary<string, string>
        {
            { "id", victimExercise.Id.ToString() }
        };

        var response = await Client.PostAsJsonAsync($"/api/exercise/{victim.Id}/deleteExercise", request);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedExercise = await db.Exercises
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(exercise => exercise.Id == victimExercise.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(responseBody, Does.Not.Contain(victimExercise.Name));
            Assert.That(persistedExercise.UserId, Is.EqualTo(victim.Id));
            Assert.That(persistedExercise.Name, Is.EqualTo(victimExercise.Name));
            Assert.That(persistedExercise.IsDeleted, Is.False);
        }
    }

    private async Task<(User Attacker, User Victim, Exercise VictimExercise)> SeedForeignExerciseScenarioAsync(
        string scenario,
        string exerciseName,
        BodyParts bodyPart)
    {
        var attacker = await SeedUserAsync(
            name: $"exercise-attacker-{scenario}",
            email: $"exercise-attacker-{scenario}@example.com");
        var victim = await SeedUserAsync(
            name: $"exercise-victim-{scenario}",
            email: $"exercise-victim-{scenario}@example.com");
        var victimExercise = await SeedExerciseAsync(victim.Id, exerciseName, bodyPart.ToString());

        return (attacker, victim, victimExercise);
    }

    private async Task<Exercise> SeedExerciseAsync(
        Id<User>? userId,
        string name,
        string bodyPart,
        bool isDeleted = false,
        ExerciseEloFormula eloFormula = ExerciseEloFormula.Standard)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Enum.TryParse<BodyParts>(bodyPart, out var bodyPartEnum);

        var exercise = new Exercise
        {
            Id = Id<Exercise>.New(),
            UserId = userId,
            Name = name,
            BodyPart = bodyPartEnum,
            EloFormula = eloFormula,
            IsDeleted = isDeleted
        };

        db.Exercises.Add(exercise);
        await db.SaveChangesAsync();

        return exercise;
    }

    private sealed class MessageResponse
    {
        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class ExerciseResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("bodyPart")]
        public BodyPartLookup? BodyPart { get; set; }

        [JsonPropertyName("eloFormula")]
        public LookupItemResponse? EloFormula { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("image")]
        public string? Image { get; set; }

        [JsonPropertyName("userId")]
        public string? UserId { get; set; }
    }

    private sealed class BodyPartLookup
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;
    }

    private sealed class LookupItemResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/{id}/addGlobalTranslation", "admin", "current-permission-allow")]
    public async Task AddGlobalTranslation_AsAdmin_AddsTranslation()
    {
        var admin = await SeedAdminAsync();
        SetAuthorizationHeader(admin.Id);

        var exercise = await SeedExerciseAsync(null, "Global Push Ups", "Chest");

        var request = new
        {
            exerciseId = exercise.Id.ToString(),
            culture = "pl",
            name = "Pompki"
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/exercise/{admin.Id}/addGlobalTranslation", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("Updated"));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/exercise/{id}/addUserExerciseWithFormula", "admin", "foreign-object-denial-no-mutation")]
    public async Task AddUserExerciseWithFormula_AsAdmin_WithForeignRoute_ReturnsForbiddenWithoutCreatingExercise()
    {
        var admin = await SeedAdminAsync();
        var foreignOwner = await SeedUserAsync(name: "formula-foreign-route-owner", email: "formula-foreign-route-owner@example.com");
        SetAuthorizationHeader(admin.Id);

        var response = await PostAsJsonWithApiOptionsAsync($"/api/exercise/{foreignOwner.Id}/addUserExerciseWithFormula", new
        {
            name = "Unauthorized Formula Exercise",
            bodyPart = BodyParts.Chest.ToString(),
            eloFormula = ExerciseEloFormula.Standard.ToString()
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That((await database.Exercises.AnyAsync(exercise => exercise.Name == "Unauthorized Formula Exercise")), Is.False);
    }

    [Test]
    public async Task AddGlobalTranslation_AsNonAdmin_ReturnsForbidden()
    {
        var user = await SeedUserAsync(name: "normaluser", email: "normal@example.com");
        var exercise = await SeedExerciseAsync(null, "Global Squats", "Quads");
        SetAuthorizationHeader(user.Id);

        var request = new
        {
            exerciseId = exercise.Id.ToString(),
            culture = "pl",
            name = "Przysiady"
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/exercise/{user.Id}/addGlobalTranslation", request);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var translationExists = await db.ExerciseTranslations
            .AsNoTracking()
            .AnyAsync(translation => translation.ExerciseId == exercise.Id && translation.Culture == "pl");
        var persistedExercise = await db.Exercises.AsNoTracking().SingleAsync(item => item.Id == exercise.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(responseBody, Does.Not.Contain(exercise.Name));
            Assert.That(responseBody, Does.Not.Contain(exercise.Id.ToString()));
            Assert.That(translationExists, Is.False);
            Assert.That(persistedExercise.Name, Is.EqualTo(exercise.Name));
            Assert.That(persistedExercise.IsDeleted, Is.False);
        }
    }

    [Test]
    public async Task AddGlobalTranslation_ForUserExercise_ReturnsForbidden()
    {
        var admin = await SeedAdminAsync();
        SetAuthorizationHeader(admin.Id);

        var exercise = await SeedExerciseAsync(admin.Id, "User Exercise for Translation", "Chest");

        var request = new
        {
            exerciseId = exercise.Id.ToString(),
            culture = "pl",
            name = "Cwiczenie uzytkownika"
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/exercise/{admin.Id}/addGlobalTranslation", request);
        var responseBody = await response.Content.ReadAsStringAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var translationExists = await db.ExerciseTranslations
            .AsNoTracking()
            .AnyAsync(translation => translation.ExerciseId == exercise.Id);
        var persistedExercise = await db.Exercises.AsNoTracking().SingleAsync(item => item.Id == exercise.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(responseBody, Does.Not.Contain(exercise.Name));
            Assert.That(responseBody, Does.Not.Contain(exercise.Id.ToString()));
            Assert.That(translationExists, Is.False);
            Assert.That(persistedExercise.UserId, Is.EqualTo(admin.Id));
            Assert.That(persistedExercise.Name, Is.EqualTo(exercise.Name));
            Assert.That(persistedExercise.IsDeleted, Is.False);
        }
    }

    [Test]
    public async Task AddGlobalTranslation_ForForeignUserExercise_ReturnsNotFound()
    {
        var admin = await SeedAdminAsync();
        var foreignOwner = await SeedUserAsync(name: "translation-foreign-owner", email: "translation-foreign-owner@example.com");
        var exercise = await SeedExerciseAsync(foreignOwner.Id, "Foreign Exercise for Translation", "Chest");
        SetAuthorizationHeader(admin.Id);

        var response = await PostAsJsonWithApiOptionsAsync($"/api/exercise/{admin.Id}/addGlobalTranslation", new
        {
            exerciseId = exercise.Id.ToString(),
            culture = "pl",
            name = "Obce cwiczenie"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task AddGlobalTranslation_WithInvalidCulture_ReturnsBadRequest()
    {
        var admin = await SeedAdminAsync();
        SetAuthorizationHeader(admin.Id);

        var exercise = await SeedExerciseAsync(null, "Global Deadlift", "Back");

        var request = new
        {
            exerciseId = exercise.Id.ToString(),
            culture = "invalid-culture-code",
            name = "Martwy ciag"
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/exercise/{admin.Id}/addGlobalTranslation", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task AddGlobalTranslation_WithMissingFields_ReturnsBadRequest()
    {
        var admin = await SeedAdminAsync();
        SetAuthorizationHeader(admin.Id);

        var request = new
        {
            exerciseId = "",
            culture = "pl",
            name = "Test"
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/exercise/{admin.Id}/addGlobalTranslation", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task AddGlobalTranslation_WithNonExistentExercise_ReturnsNotFound()
    {
        var admin = await SeedAdminAsync();
        SetAuthorizationHeader(admin.Id);

        var nonExistentId = Id<Exercise>.New();
        var request = new
        {
            exerciseId = nonExistentId.ToString(),
            culture = "pl",
            name = "Test"
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/exercise/{admin.Id}/addGlobalTranslation", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
