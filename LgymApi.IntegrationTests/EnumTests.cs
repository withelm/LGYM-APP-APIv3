using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LgymApi.Domain.Enums;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class EnumTests : IntegrationTestBase
{
    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/enums", "authenticated-global", "ordinary-authenticated-allow")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/enums/all", "authenticated-global", "ordinary-authenticated-allow")]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/enums/{enumType}", "authenticated-global", "ordinary-authenticated-allow")]
    public async Task EnumRoutes_WithAuthenticatedUser_ReturnReferenceData()
    {
        var (userId, _) = await RegisterUserViaEndpointAsync("enum-auth", "enum-auth@example.com", "password123");
        SetAuthorizationHeader(userId);

        using var types = await Client.GetAsync("/api/enums");
        using var all = await Client.GetAsync("/api/enums/all");
        using var bodyParts = await Client.GetAsync("/api/enums/BodyParts");

        Assert.That(types.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(all.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(bodyParts.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GetEnumLookup_DoesNotReturnHiddenUnknownValue()
    {
        var (userId, _) = await RegisterUserViaEndpointAsync(
            name: "enumuser1",
            email: "enum1@example.com",
            password: "password123");
        SetAuthorizationHeader(userId);

        var response = await Client.GetAsync("/api/enums/BodyParts");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<EnumLookupResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.Values.Select(value => value.Id), Is.EqualTo(System.Enum.GetNames<BodyParts>().Where(name => name != "Unknown")));
        Assert.That((body.Values).Any(value => value.Id == "Unknown" || value.Name == "Unknown"), Is.False);
    }

    [TestCase("en", "enumuser3en", "enum3-en@example.com", "Pull-up weighted")]
    [TestCase("pl", "enumuser3pl", "enum3-pl@example.com", "Podciąganie: im mniejszy ciężar, tym lepiej")]
    public async Task GetEnumLookup_Returns_Exact_PullupWeighted_Wire_Values_For_Culture(
        string culture,
        string userName,
        string email,
        string expectedLabel)
    {
        var (userId, _) = await RegisterUserViaEndpointAsync(
            name: userName,
            email: email,
            password: "password123");
        SetAuthorizationHeader(userId);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/enums/ExerciseEloFormula");
        request.Headers.AcceptLanguage.ParseAdd(culture);
        var response = await Client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<EnumLookupResponse>();
        Assert.That(body, Is.Not.Null);
        var pullupWeighted = body!.Values.Single(value => value.Id == "PullupWeighted");
        Assert.That(pullupWeighted.Id, Is.EqualTo("PullupWeighted"));
        Assert.That(pullupWeighted.Name, Is.EqualTo(expectedLabel));
        Assert.That(pullupWeighted.DisplayName, Is.EqualTo(expectedLabel));
    }

    [Test]
    public async Task GetEnumLookup_Matches_Enum_Type_Case_Insensitively()
    {
        var (userId, _) = await RegisterUserViaEndpointAsync(
            name: "enumusercase",
            email: "enum-case@example.com",
            password: "password123");
        SetAuthorizationHeader(userId);

        var response = await Client.GetAsync("/api/enums/eXeRcIsEeLoFoRmUlA");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<EnumLookupResponse>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body!.EnumType, Is.EqualTo(nameof(ExerciseEloFormula)));
        Assert.That(body.Values.Select(value => value.Id), Does.Contain("PullupWeighted"));
    }

    [Test]
    public async Task GetAllEnumLookups_DoesNotReturnHiddenUnknownValues()
    {
        var (userId, _) = await RegisterUserViaEndpointAsync(
            name: "enumuser2",
            email: "enum2@example.com",
            password: "password123");
        SetAuthorizationHeader(userId);

        var response = await Client.GetAsync("/api/enums/all");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<List<EnumLookupResponse>>();
        Assert.That(body, Is.Not.Null);
        Assert.That((body!.SelectMany(lookup => lookup.Values)).Any(value => value.Id == "Unknown" || value.Name == "Unknown"), Is.False);
    }

    private sealed class EnumLookupResponse
    {
        [JsonPropertyName("enumType")]
        public string EnumType { get; set; } = string.Empty;

        [JsonPropertyName("values")]
        public List<EnumLookupValue> Values { get; set; } = new();
    }

    private sealed class EnumLookupValue
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;
    }
}
