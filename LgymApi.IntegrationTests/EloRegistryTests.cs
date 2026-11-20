using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class EloRegistryTests : IntegrationTestBase
{
    [Test]
    public async Task Register_CreatesInitialEloEntry()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "elouser",
            email: "elo@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync($"/api/userInfo/{userId}/getUserEloPoints");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<EloResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Elo, Is.EqualTo(1000));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/eloRegistry/{id}/getEloRegistryChart", "own", "owner-allow")]
    public async Task GetEloRegistryChart_WithSingleEntry_ReturnsChart()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "chartuser",
            email: "chart@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync($"/api/eloRegistry/{userId}/getEloRegistryChart");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<List<EloChartEntry>>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(body![0].Value, Is.EqualTo(1000));
        Assert.That(Id<LgymApi.Domain.Entities.EloRegistry>.TryParse(body[0].Id, out var parsedId), Is.True);
        Assert.That(parsedId.ToString(), Is.EqualTo(body[0].Id));

        var repeatResponse = await Client.GetAsync($"/api/eloRegistry/{userId}/getEloRegistryChart");
        Assert.That(repeatResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var repeatBody = await repeatResponse.Content.ReadFromJsonAsync<List<EloChartEntry>>();
        Assert.That(repeatBody, Is.Not.Null);
        Assert.That(repeatBody!.Select(entry => entry.Id), Is.EqualTo(body.Select(entry => entry.Id)));
        TestContext.Progress.WriteLine(System.Text.Json.JsonSerializer.Serialize(body));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/eloRegistry/{id}/getEloRegistryChart", "own", "foreign-object-denial-no-mutation")]
    public async Task GetEloRegistryChart_WithMismatchedRouteUserId_ReturnsForbidden()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "authuser",
            email: "auth@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var (otherUserId, _) = await RegisterUserViaEndpointAsync(
            name: "eloother",
            email: "eloother@example.com",
            password: "password123");

        var response = await Client.GetAsync($"/api/eloRegistry/{otherUserId}/getEloRegistryChart");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task GetEloRegistryChart_WithInvalidGuidFormat_ReturnsForbidden()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "authuser2",
            email: "auth2@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync("/api/eloRegistry/invalid-guid/getEloRegistryChart");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task GetUserEloPoints_WithValidUser_ReturnsElo()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "elopointsuser",
            email: "elopoints@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync($"/api/userInfo/{userId}/getUserEloPoints");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<EloResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Elo, Is.EqualTo(1000));
    }

    [Test]
    public async Task GetUserEloPoints_WithInvalidUserId_ReturnsNotFound()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "authuser3",
            email: "auth3@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var nonExistentId = Domain.ValueObjects.Id<object>.New();
        var response = await Client.GetAsync($"/api/userInfo/{nonExistentId}/getUserEloPoints");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Login_ReturnsCurrentElo()
    {
        await RegisterUserViaEndpointAsync(
            name: "loginuser",
            email: "login@example.com",
            password: "password123");

        var loginRequest = new { name = "loginuser", password = "password123" };
        var response = await Client.PostAsJsonAsync("/api/login", loginRequest);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.User, Is.Not.Null);
        Assert.That(body.User!.Elo, Is.EqualTo(1000));
    }

    [Test]
    public async Task CheckToken_ReturnsCurrentElo()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "tokenuser",
            email: "token@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync("/api/checkToken");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<UserInfoResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Elo, Is.EqualTo(1000));
    }

    private sealed class EloResponse
    {
        [JsonPropertyName("elo")]
        public int Elo { get; set; }
    }

    private sealed class EloChartEntry
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public int Value { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;
    }

    private sealed class LoginResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("req")]
        public UserInfoResponse? User { get; set; }
    }

    private sealed class UserInfoResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("elo")]
        public int Elo { get; set; }
    }
}
