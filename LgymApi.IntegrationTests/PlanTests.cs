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
public sealed class PlanTests : IntegrationTestBase
{
    [Test]
    public async Task CreatePlan_WithValidData_CreatesPlanAndAssignsToUser()
    {
        var user = await SeedUserAsync(name: "planuser", email: "plan@example.com");
        SetAuthorizationHeader(user.Id);

        var request = new
        {
            name = "My Workout Plan"
        };

        var response = await Client.PostAsJsonAsync($"/api/{user.Id}/createPlan", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        body.Should().NotBeNull();
        body!.Message.Should().Be("Created");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Name == "My Workout Plan" && p.UserId == user.Id);
        plan.Should().NotBeNull();
        plan!.IsActive.Should().BeTrue();

        var updatedUser = await db.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser!.PlanId.Should().Be(plan.Id);
    }

    [Test]
    public async Task CreatePlan_WithMismatchedUserId_ReturnsForbidden()
    {
        var user1 = await SeedUserAsync(name: "user1", email: "user1@example.com");
        var user2 = await SeedUserAsync(name: "user2", email: "user2@example.com");
        SetAuthorizationHeader(user1.Id);

        var request = new
        {
            name = "Plan"
        };

        var response = await Client.PostAsJsonAsync($"/api/{user2.Id}/createPlan", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task UpdatePlan_WithValidData_UpdatesPlanName()
    {
        var user = await SeedUserAsync(name: "planuser", email: "plan@example.com");
        var plan = await SeedPlanAsync(user.Id, "Old Name");
        SetAuthorizationHeader(user.Id);

        var request = new
        {
            _id = plan.Id.ToString(),
            name = "New Name"
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/{user.Id}/updatePlan", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var updatedPlan = await db.Plans.FirstOrDefaultAsync(p => p.Id == plan.Id);
        updatedPlan.Should().NotBeNull();
        updatedPlan!.Name.Should().Be("New Name");
    }

    [Test]
    public async Task GetPlanConfig_WithActivePlan_ReturnsPlan()
    {
        var user = await SeedUserAsync(name: "planuser", email: "plan@example.com");
        var plan = await SeedPlanAsync(user.Id, "Active Plan", isActive: true);
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync($"/api/{user.Id}/getPlanConfig");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PlanFormResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(plan.Id.ToString());
        body.Name.Should().Be("Active Plan");
        body.IsActive.Should().BeTrue();
    }

    [Test]
    public async Task GetPlanConfig_WithNoActivePlan_ReturnsNotFound()
    {
        var user = await SeedUserAsync(name: "planuser", email: "plan@example.com");
        await SeedPlanAsync(user.Id, "Inactive Plan", isActive: false);
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync($"/api/{user.Id}/getPlanConfig");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CheckIsUserHavePlan_WithNoPlan_ReturnsFalse()
    {
        var user = await SeedUserAsync(name: "planuser", email: "plan@example.com");
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync($"/api/{user.Id}/checkIsUserHavePlan");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<bool>();
        result.Should().BeFalse();
    }

    [Test]
    public async Task GetPlansList_WithMultiplePlans_ReturnsAllPlans()
    {
        var user = await SeedUserAsync(name: "planuser", email: "plan@example.com");
        await SeedPlanAsync(user.Id, "Plan 1", isActive: true);
        await SeedPlanAsync(user.Id, "Plan 2", isActive: false);
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync($"/api/{user.Id}/getPlansList");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<PlanFormResponse>>();
        body.Should().NotBeNull();
        body.Should().HaveCount(2);
        body!.Select(p => p.Name).Should().Contain(new[] { "Plan 1", "Plan 2" });
    }

    [Test]
    public async Task GetPlansList_WithNoPlans_ReturnsNotFound()
    {
        var user = await SeedUserAsync(name: "planuser", email: "plan@example.com");
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync($"/api/{user.Id}/getPlansList");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task SetNewActivePlan_WithValidPlanId_SetsActivePlan()
    {
        var user = await SeedUserAsync(name: "planuser", email: "plan@example.com");
        var plan1 = await SeedPlanAsync(user.Id, "Plan 1", isActive: true);
        var plan2 = await SeedPlanAsync(user.Id, "Plan 2", isActive: false);
        SetAuthorizationHeader(user.Id);

        var request = new
        {
            _id = plan2.Id.ToString()
        };

        var response = await PostAsJsonWithApiOptionsAsync($"/api/{user.Id}/setNewActivePlan", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var updatedPlan1 = await db.Plans.FirstOrDefaultAsync(p => p.Id == plan1.Id);
        var updatedPlan2 = await db.Plans.FirstOrDefaultAsync(p => p.Id == plan2.Id);

        updatedPlan1!.IsActive.Should().BeFalse();
        updatedPlan2!.IsActive.Should().BeTrue();
    }

    private async Task<Plan> SeedPlanAsync(Guid userId, string name, bool isActive = true)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            IsActive = isActive
        };

        db.Plans.Add(plan);
        await db.SaveChangesAsync();

        return plan;
    }

    private sealed class MessageResponse
    {
        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class PlanFormResponse
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }
    }

    private sealed class ShareCodeResponse
    {
        [JsonPropertyName("shareCode")]
        public string ShareCode { get; set; } = string.Empty;
    }

    private sealed class CopiedPlanResponse
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }
    }

    [Test]
    public async Task GenerateShareCode_WithValidPlan_ReturnsShareCode()
    {
        var user = await SeedUserAsync(name: "shareuser", email: "share@example.com");
        var plan = await SeedPlanAsync(user.Id, "Shareable Plan");
        SetAuthorizationHeader(user.Id);

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/{plan.Id}/share");
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ShareCodeResponse>();
        body.Should().NotBeNull();
        body!.ShareCode.Should().NotBeNullOrWhiteSpace();
        body.ShareCode.Should().HaveLength(10);
    }

    [Test]
    public async Task GenerateShareCode_WithInvalidPlanId_ReturnsNotFound()
    {
        var user = await SeedUserAsync(name: "shareuser2", email: "share2@example.com");
        SetAuthorizationHeader(user.Id);

        var nonExistentPlanId = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/{nonExistentPlanId}/share");
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GenerateShareCode_WithOtherUsersPlan_ReturnsForbidden()
    {
        var user1 = await SeedUserAsync(name: "shareuser3", email: "share3@example.com");
        var user2 = await SeedUserAsync(name: "shareuser4", email: "share4@example.com");
        var plan = await SeedPlanAsync(user2.Id, "Other User Plan");
        SetAuthorizationHeader(user1.Id);

        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/{plan.Id}/share");
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task CopyPlan_WithValidShareCode_CopiesPlan()
    {
        var user1 = await SeedUserAsync(name: "copyuser1", email: "copy1@example.com");
        var user2 = await SeedUserAsync(name: "copyuser2", email: "copy2@example.com");
        var plan = await SeedPlanAsync(user1.Id, "Plan To Copy");
        
        SetAuthorizationHeader(user1.Id);
        var shareRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/{plan.Id}/share");
        var shareResponse = await Client.SendAsync(shareRequest);
        var shareBody = await shareResponse.Content.ReadFromJsonAsync<ShareCodeResponse>();
        var shareCode = shareBody!.ShareCode;

        SetAuthorizationHeader(user2.Id);
        var copyRequest = new { shareCode };
        var copyResponse = await Client.PostAsJsonAsync("/api/copy", copyRequest);

        copyResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var copyBody = await copyResponse.Content.ReadFromJsonAsync<CopiedPlanResponse>();
        copyBody.Should().NotBeNull();
        copyBody!.Name.Should().Be("Plan To Copy");
        copyBody.UserId.Should().Be(user2.Id);
        copyBody.IsActive.Should().BeTrue();
    }

    [Test]
    public async Task CopyPlan_WithInvalidShareCode_ReturnsNotFound()
    {
        var user = await SeedUserAsync(name: "copyuser3", email: "copy3@example.com");
        SetAuthorizationHeader(user.Id);

        var copyRequest = new { shareCode = "INVALID1" };
        var copyResponse = await Client.PostAsJsonAsync("/api/copy", copyRequest);

        copyResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CopyPlan_WithoutAuth_ReturnsUnauthorized()
    {
        ClearAuthorizationHeader();

        var copyRequest = new { shareCode = "TESTCODE" };
        var copyResponse = await Client.PostAsJsonAsync("/api/copy", copyRequest);

        copyResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GenerateShareCode_CalledTwice_ReturnsSameCode()
    {
        var user = await SeedUserAsync(name: "sharetwice", email: "sharetwice@example.com");
        var plan = await SeedPlanAsync(user.Id, "Double Share Plan");
        SetAuthorizationHeader(user.Id);

        var request1 = new HttpRequestMessage(HttpMethod.Patch, $"/api/{plan.Id}/share");
        var response1 = await Client.SendAsync(request1);
        var body1 = await response1.Content.ReadFromJsonAsync<ShareCodeResponse>();

        var request2 = new HttpRequestMessage(HttpMethod.Patch, $"/api/{plan.Id}/share");
        var response2 = await Client.SendAsync(request2);
        var body2 = await response2.Content.ReadFromJsonAsync<ShareCodeResponse>();

        body1!.ShareCode.Should().Be(body2!.ShareCode);
    }
}
