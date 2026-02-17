using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

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
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;
    }
}
