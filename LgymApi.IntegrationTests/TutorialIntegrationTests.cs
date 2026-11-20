using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LgymApi.Api.Features.Tutorial.Contracts;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Application.Features.Tutorial;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class TutorialIntegrationTests : IntegrationTestBase
{


    private async Task InitializeTutorialAsync(Id<User> userId)
    {
        using var scope = Factory.Services.CreateScope();
        var tutorialService = scope.ServiceProvider.GetRequiredService<ITutorialService>();
        await tutorialService.InitializeOnboardingTutorialAsync(userId);
    }
    [Test]
    public async Task RegisteredUser_HasActiveTutorials()
    {
        // Register a new user
        var registerRequest = new
        {
            name = "tutorial-user",
            email = "tutorial@example.com",
            password = "password123",
            cpassword = "password123",
            isVisibleInRanking = true
        };

        SetIdempotencyKey("test-register-tutorial-user");
        var registerResponse = await Client.PostAsJsonAsync("/api/register", registerRequest);
        ClearIdempotencyKey();

        Assert.That(registerResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var registeredUser = await db.Users.FirstAsync(u => u.Email == "tutorial@example.com");
        var tutorial = await db.UserTutorialProgresses
            .AsNoTracking()
            .SingleAsync(progress => progress.UserId == registeredUser.Id && progress.TutorialType == TutorialType.OnboardingDemo);
        Assert.That(tutorial.IsCompleted, Is.False);

        // Login to get token and user info
        var loginRequest = new
        {
            name = "tutorial-user",
            password = "password123"
        };

        var loginResponse = await Client.PostAsJsonAsync("/api/login", loginRequest);
        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(SharedSerializationOptions.Current);
        Assert.That(loginBody, Is.Not.Null);
        Assert.That(string.IsNullOrWhiteSpace(loginBody!.Token), Is.False);
        Assert.That(loginBody.User, Is.Not.Null);
        Assert.That(loginBody.User!.HasActiveTutorials, Is.True);
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/tutorials/active", "own", "owner-allow")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/tutorials/active", "own", "no-client-subject")]
    public async Task GetActiveTutorials_ReturnsOnboardingTutorial()
    {
        // Seed user and initialize tutorial
        var user = await SeedUserAsync(name: "active-tutorial-user", email: "active@example.com");
        await InitializeTutorialAsync(user.Id);
        SetAuthorizationHeader(user.Id);

        // Get active tutorials
        var response = await Client.GetAsync("/api/tutorials/active");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<List<TutorialProgressDto>>(SharedSerializationOptions.Current);
        Assert.That(body, Is.Not.Null);
        Assert.That(body, Has.Exactly(1).Items);
        Assert.That(body![0].TutorialType, Is.EqualTo(TutorialType.OnboardingDemo));
        Assert.That(body[0].IsCompleted, Is.False);
        Assert.That(body[0].TotalSteps, Is.EqualTo(6));
        Assert.That(body[0].CompletedSteps, Is.Empty);
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/tutorials/{tutorialType}", "own", "owner-allow")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/tutorials/{tutorialType}", "own", "no-client-subject")]
    public async Task GetTutorialProgress_ReturnsProgressWithZeroCompletedSteps()
    {
        // Seed user and initialize tutorial
        var user = await SeedUserAsync(name: "progress-user", email: "progress@example.com");
        await InitializeTutorialAsync(user.Id);
        SetAuthorizationHeader(user.Id);

        // Get tutorial progress
        var response = await Client.GetAsync("/api/tutorials/OnboardingDemo");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<TutorialProgressDto>(SharedSerializationOptions.Current);
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.TutorialType, Is.EqualTo(TutorialType.OnboardingDemo));
        Assert.That(body.IsCompleted, Is.False);
        Assert.That(body.CompletedSteps, Is.Empty);
        Assert.That(body.RemainingSteps, Has.Exactly(6).Items);
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/tutorials/completeStep", "own", "owner-allow")]
    public async Task CompleteStep_UpdatesProgressAndMarksStepComplete()
    {
        // Seed user and initialize tutorial
        var user = await SeedUserAsync(name: "step-complete-user", email: "step@example.com");
        await InitializeTutorialAsync(user.Id);
        SetAuthorizationHeader(user.Id);

        // Complete first step
        var completeStepRequest = new CompleteStepRequest
        {
            TutorialType = TutorialType.OnboardingDemo,
            Step = TutorialStep.CreateArea
        };

        var completeResponse = await PostAsJsonWithApiOptionsAsync("/api/tutorials/completeStep", completeStepRequest);
        Assert.That(completeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Get updated progress
        var progressResponse = await Client.GetAsync("/api/tutorials/OnboardingDemo");
        Assert.That(progressResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var progress = await progressResponse.Content.ReadFromJsonAsync<TutorialProgressDto>(SharedSerializationOptions.Current);
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.CompletedSteps, Has.Exactly(1).Items);
        Assert.That(progress!.CompletedSteps, Does.Contain(TutorialStep.CreateArea));
        Assert.That(progress.RemainingSteps, Has.Exactly(5).Items);
        Assert.That(progress.IsCompleted, Is.False);
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/tutorials/complete", "own", "owner-allow")]
    public async Task CompleteTutorial_MarksInactiveAndClearsActiveTutorials()
    {
        // Seed user and initialize tutorial
        var user = await SeedUserAsync(name: "complete-tutorial-user", email: "complete@example.com");
        await InitializeTutorialAsync(user.Id);
        SetAuthorizationHeader(user.Id);

        // Complete tutorial
        var completeTutorialRequest = new CompleteTutorialRequest
        {
            TutorialType = TutorialType.OnboardingDemo
        };

        var completeResponse = await PostAsJsonWithApiOptionsAsync("/api/tutorials/complete", completeTutorialRequest);
        Assert.That(completeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Get active tutorials - should be empty
        var activeResponse = await Client.GetAsync("/api/tutorials/active");
        Assert.That(activeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var activeTutorials = await activeResponse.Content.ReadFromJsonAsync<List<TutorialProgressDto>>(SharedSerializationOptions.Current);
        Assert.That(activeTutorials, Is.Not.Null);
        Assert.That(activeTutorials, Is.Empty);

        // Check token - hasActiveTutorials should be false
        var checkTokenResponse = await Client.GetAsync("/api/checkToken");
        Assert.That(checkTokenResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var tokenUser = await checkTokenResponse.Content.ReadFromJsonAsync<UserDto>(SharedSerializationOptions.Current);
        Assert.That(tokenUser, Is.Not.Null);
        Assert.That(tokenUser!.HasActiveTutorials, Is.False);
    }

    [Test]
    public async Task CompleteAllSteps_MarksCompletedAndHasActiveTutorialsFalse()
    {
        // Seed user and initialize tutorial
        var user = await SeedUserAsync(name: "complete-all-steps-user", email: "allsteps@example.com");
        await InitializeTutorialAsync(user.Id);
        SetAuthorizationHeader(user.Id);

        // Complete all 6 steps
        var steps = new[]
        {
            TutorialStep.CreateArea,
            TutorialStep.CreateGym,
            TutorialStep.CreatePlan,
            TutorialStep.CreatePlanDay,
            TutorialStep.CreateTraining,
            TutorialStep.LastTreningResult
        };

        foreach (var step in steps)
        {
            var completeStepRequest = new CompleteStepRequest
            {
                TutorialType = TutorialType.OnboardingDemo,
                Step = step
            };

            var stepResponse = await PostAsJsonWithApiOptionsAsync("/api/tutorials/completeStep", completeStepRequest);
            Assert.That(stepResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        // Get tutorial progress - should be completed
        var progressResponse = await Client.GetAsync("/api/tutorials/OnboardingDemo");
        Assert.That(progressResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var progress = await progressResponse.Content.ReadFromJsonAsync<TutorialProgressDto>(SharedSerializationOptions.Current);
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress!.CompletedSteps, Has.Exactly(6).Items);
        Assert.That(progress.IsCompleted, Is.True);

        // Get active tutorials - should be empty
        var activeResponse = await Client.GetAsync("/api/tutorials/active");
        Assert.That(activeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var activeTutorials = await activeResponse.Content.ReadFromJsonAsync<List<TutorialProgressDto>>(SharedSerializationOptions.Current);
        Assert.That(activeTutorials, Is.Not.Null);
        Assert.That(activeTutorials, Is.Empty);

        // Check token - hasActiveTutorials should be false
        var checkTokenResponse = await Client.GetAsync("/api/checkToken");
        Assert.That(checkTokenResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var tokenUser = await checkTokenResponse.Content.ReadFromJsonAsync<UserDto>(SharedSerializationOptions.Current);
        Assert.That(tokenUser, Is.Not.Null);
        Assert.That(tokenUser!.HasActiveTutorials, Is.False);
    }

    [Test]
    public async Task CompleteTutorial_Idempotent_CanBeCalledMultipleTimes()
    {
        // Seed user and initialize tutorial
        var user = await SeedUserAsync(name: "idempotent-user", email: "idempotent@example.com");
        await InitializeTutorialAsync(user.Id);
        SetAuthorizationHeader(user.Id);

        var completeTutorialRequest = new CompleteTutorialRequest
        {
            TutorialType = TutorialType.OnboardingDemo
        };

        // Complete tutorial first time
        var firstResponse = await PostAsJsonWithApiOptionsAsync("/api/tutorials/complete", completeTutorialRequest);
        Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Complete tutorial second time - should not fail
        var secondResponse = await PostAsJsonWithApiOptionsAsync("/api/tutorials/complete", completeTutorialRequest);
        Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Verify still completed and active list is empty
        var activeResponse = await Client.GetAsync("/api/tutorials/active");
        Assert.That(activeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var activeTutorials = await activeResponse.Content.ReadFromJsonAsync<List<TutorialProgressDto>>(SharedSerializationOptions.Current);
        Assert.That(activeTutorials, Is.Not.Null);
        Assert.That(activeTutorials, Is.Empty);
    }
}

// Response DTOs for deserialization

public sealed class LoginResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = null!;

    [JsonPropertyName("req")]
    public UserDto? User { get; set; }
}

public sealed class UserDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; } = null!;

    [JsonPropertyName("hasActiveTutorials")]
    public bool HasActiveTutorials { get; set; }
}
