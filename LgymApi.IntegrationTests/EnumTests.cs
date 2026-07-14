using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LgymApi.Domain.Enums;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class EnumTests : IntegrationTestBase
{
    [Test]
    public async Task GetEnumLookup_DoesNotReturnHiddenUnknownValue()
    {
        var (userId, _) = await RegisterUserViaEndpointAsync(
            name: "enumuser1",
            email: "enum1@example.com",
            password: "password123");
        SetAuthorizationHeader(userId);

        var response = await Client.GetAsync("/api/enums/BodyParts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<EnumLookupResponse>();
        body.Should().NotBeNull();
        body!.Values.Select(v => v.Name).Should().NotContain("Unknown");
    }

    [Test]
    public async Task GetEnumLookup_Returns_Enum_String_Id_And_Translated_Labels()
    {
        var (userId, _) = await RegisterUserViaEndpointAsync(
            name: "enumuser3",
            email: "enum3@example.com",
            password: "password123");
        SetAuthorizationHeader(userId);

        var response = await Client.GetAsync("/api/enums/ExerciseEloFormula");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<EnumLookupResponse>();
        body.Should().NotBeNull();
        var pullupWeighted = body!.Values.Single(v => v.Id == ExerciseEloFormula.PullupWeighted.ToString());
        pullupWeighted.DisplayName.Should().NotBeNullOrWhiteSpace();
        pullupWeighted.DisplayName.Should().NotBe(ExerciseEloFormula.PullupWeighted.ToString());
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

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<EnumLookupResponse>>();
        body.Should().NotBeNull();
        body!.SelectMany(x => x.Values).Select(v => v.Name).Should().NotContain("Unknown");
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
