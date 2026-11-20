using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class RankingTests : IntegrationTestBase
{
    [Test]
    public async Task GetUsersRanking_WithNoUsers_ReturnsNotFound()
    {
        var user = await SeedUserAsync(name: "authuser", email: "auth@example.com", isTester: true);
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync("/api/getUsersRanking");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("Not found."));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/getUsersRanking", "authenticated-global", "ordinary-authenticated-allow")]
    public async Task GetUsersRanking_WithVisibleUsers_ReturnsRankedUsers()
    {
        var user1 = await SeedUserAsync(name: "highelo", email: "high@example.com", elo: 2000);
        var user2 = await SeedUserAsync(name: "lowelo", email: "low@example.com", elo: 1200);
        SetAuthorizationHeader(user1.Id);

        var response = await Client.GetAsync("/api/getUsersRanking");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<List<UserRankingResponse>>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body, Has.Exactly(2).Items);
        Assert.That(body![0].Name, Is.EqualTo("highelo"));
        Assert.That(body[0].Elo, Is.EqualTo(2000));
        Assert.That(body[1].Name, Is.EqualTo("lowelo"));
        Assert.That(body[1].Elo, Is.EqualTo(1200));
    }

    [Test]
    public async Task GetUsersRanking_ExcludesHiddenUsers()
    {
        var visibleUser = await SeedUserAsync(name: "visible", email: "visible@example.com", elo: 1500, isVisibleInRanking: true);
        await SeedUserAsync(name: "hidden", email: "hidden@example.com", elo: 2000, isVisibleInRanking: false);
        SetAuthorizationHeader(visibleUser.Id);

        var response = await Client.GetAsync("/api/getUsersRanking");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<List<UserRankingResponse>>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body, Has.Exactly(1).Items);
        Assert.That(body![0].Name, Is.EqualTo("visible"));
    }

    [Test]
    public async Task GetUsersRanking_ExcludesTesters()
    {
        var normalUser = await SeedUserAsync(name: "normal", email: "normal@example.com", elo: 1500);
        await SeedUserAsync(name: "tester", email: "tester@example.com", elo: 3000, isTester: true);
        SetAuthorizationHeader(normalUser.Id);

        var response = await Client.GetAsync("/api/getUsersRanking");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<List<UserRankingResponse>>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body, Has.Exactly(1).Items);
        Assert.That(body![0].Name, Is.EqualTo("normal"));
    }

    [Test]
    public async Task GetUsersRanking_ExcludesDeletedUsers()
    {
        var activeUser = await SeedUserAsync(name: "active", email: "active@example.com", elo: 1500);
        await SeedUserAsync(name: "deleted", email: "deleted@example.com", elo: 2500, isDeleted: true);
        SetAuthorizationHeader(activeUser.Id);

        var response = await Client.GetAsync("/api/getUsersRanking");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<List<UserRankingResponse>>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body, Has.Exactly(1).Items);
        Assert.That(body![0].Name, Is.EqualTo("active"));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/changeVisibilityInRanking", "own", "owner-allow")]
    public async Task ChangeVisibilityInRanking_SetsVisibilityToFalse()
    {
        var user = await SeedUserAsync(name: "visibilityuser", email: "vis@example.com", isVisibleInRanking: true);
        SetAuthorizationHeader(user.Id);

        var request = new ChangeVisibilityInRankingRequest { IsVisibleInRanking = false };
        var response = await Client.PostAsJsonAsync("/api/changeVisibilityInRanking", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("Updated"));

        var rankingResponse = await Client.GetAsync("/api/getUsersRanking");
        Assert.That(rankingResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task ChangeVisibilityInRanking_SetsVisibilityToTrue()
    {
        var user = await SeedUserAsync(name: "hiddenuser", email: "hid@example.com", isVisibleInRanking: false);
        SetAuthorizationHeader(user.Id);

        var request = new ChangeVisibilityInRankingRequest { IsVisibleInRanking = true };
        var response = await Client.PostAsJsonAsync("/api/changeVisibilityInRanking", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("Updated"));

        var rankingResponse = await Client.GetAsync("/api/getUsersRanking");
        Assert.That(rankingResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var rankings = await rankingResponse.Content.ReadFromJsonAsync<List<UserRankingResponse>>();
        Assert.That((rankings).Where(r => r.Name == "hiddenuser"), Has.Exactly(1).Items);
    }

    [Test]
    public async Task ChangeVisibilityInRanking_WithoutRequiredField_ReturnsBadRequest()
    {
        var user = await SeedUserAsync(name: "badrequest", email: "bad@example.com");
        SetAuthorizationHeader(user.Id);

        var request = new ChangeVisibilityInRankingRequest { IsVisibleInRanking = null };
        var response = await Client.PostAsJsonAsync("/api/changeVisibilityInRanking", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/userInfo/{id}/getUserEloPoints", "authenticated-global", "ordinary-authenticated-allow")]
    public async Task GetUserEloPoints_WithValidUser_ReturnsElo()
    {
        var user = await SeedUserAsync(name: "elouser", email: "elo@example.com", elo: 1750);
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync($"/api/userInfo/{user.Id}/getUserEloPoints");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<EloResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Elo, Is.EqualTo(1750));
    }

    [Test]
    public async Task GetUserEloPoints_WithInvalidUserId_ReturnsNotFound()
    {
        var user = await SeedUserAsync(name: "authuser", email: "auth@example.com");
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync($"/api/userInfo/{Id<User>.New()}/getUserEloPoints");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("Not found."));
    }

    [Test]
    public async Task GetUserEloPoints_WithInvalidGuidFormat_ReturnsBadRequest()
    {
        var user = await SeedUserAsync(name: "authuser", email: "auth@example.com");
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync("/api/userInfo/invalid-guid/getUserEloPoints");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Message, Is.EqualTo("Not found."));
    }

    private sealed class MessageResponse
    {
        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class UserRankingResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }

        [JsonPropertyName("elo")]
        public int Elo { get; set; }

        [JsonPropertyName("profileRank")]
        public string ProfileRank { get; set; } = string.Empty;
    }

    private sealed class EloResponse
    {
        [JsonPropertyName("elo")]
        public int Elo { get; set; }
    }
}
