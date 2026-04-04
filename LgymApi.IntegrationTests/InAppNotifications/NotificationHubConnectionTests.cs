using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace LgymApi.IntegrationTests.InAppNotifications;

[TestFixture]
public sealed class NotificationHubConnectionTests : IntegrationTestBase
{
    [Test]
    public async Task Hub_Unauthenticated_Returns401()
    {
        ClearAuthorizationHeader();

        var response = await Client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Hub_AuthenticatedUser_CanConnect()
    {
        var user = await SeedUserAsync(name: "hub-user", email: "hub-user@example.com");
        SetAuthorizationHeader(user.Id);

        var response = await Client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<NegotiateResponse>();
        body.Should().NotBeNull();
        body!.ConnectionId.Should().NotBeNullOrWhiteSpace();
        body.AvailableTransports.Should().NotBeNull();
        body.AvailableTransports.Should().NotBeEmpty();
    }

    private sealed class NegotiateResponse
    {
        [JsonPropertyName("connectionId")]
        public string ConnectionId { get; set; } = string.Empty;

        [JsonPropertyName("availableTransports")]
        public List<AvailableTransportResponse> AvailableTransports { get; set; } = [];
    }

    private sealed class AvailableTransportResponse
    {
        [JsonPropertyName("transport")]
        public string Transport { get; set; } = string.Empty;
    }
}
