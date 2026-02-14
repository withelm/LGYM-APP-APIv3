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
}
