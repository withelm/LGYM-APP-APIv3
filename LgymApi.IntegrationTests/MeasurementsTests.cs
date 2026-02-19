using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using LgymApi.Domain.Enums;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class MeasurementsTests : IntegrationTestBase
{
    [Test]
    public async Task AddMeasurement_WithValidData_CreatesMeasurement()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "measureuser",
            email: "measure@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            bodyPart = BodyParts.Chest.ToString(),
            value = 100.5,
            unit = HeightUnits.Centimeters.ToString()
        };

        var response = await Client.PostAsJsonAsync("/api/measurements/add", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        body.Should().NotBeNull();
        body!.Message.Should().Be("Created");
    }

    [Test]
    public async Task AddMeasurement_WithoutAuth_ReturnsNotFound()
    {
        ClearAuthorizationHeader();

        var request = new
        {
            bodyPart = BodyParts.Chest.ToString(),
            value = 100.5,
            unit = HeightUnits.Centimeters.ToString()
        };

        var response = await Client.PostAsJsonAsync("/api/measurements/add", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AddMeasurement_WithInvalidBodyPart_ReturnsBadRequest()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "measureuser2",
            email: "measure2@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            bodyPart = "InvalidBodyPart",
            value = 50.0,
            unit = HeightUnits.Centimeters.ToString()
        };

        var response = await Client.PostAsJsonAsync("/api/measurements/add", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetMeasurementsHistory_WithNoMeasurements_ReturnsNotFound()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "historyuser",
            email: "history@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync($"/api/measurements/{userId}/getHistory");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetMeasurementsHistory_WithMeasurements_ReturnsHistory()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "historyuser2",
            email: "history2@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var addRequest = new
        {
            bodyPart = BodyParts.Biceps.ToString(),
            value = 35.0,
            unit = HeightUnits.Centimeters.ToString()
        };
        await Client.PostAsJsonAsync("/api/measurements/add", addRequest);

        var response = await Client.GetAsync($"/api/measurements/{userId}/getHistory");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MeasurementsHistoryResponse>();
        body.Should().NotBeNull();
        body!.Measurements.Should().HaveCountGreaterThanOrEqualTo(1);
        body.Measurements[0].Value.Should().Be(35.0);
    }

    [Test]
    public async Task GetMeasurementsHistory_WithBodyPartFilter_FiltersCorrectly()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "filteruser",
            email: "filter@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var chestRequest = new { bodyPart = BodyParts.Chest.ToString(), value = 100.0, unit = HeightUnits.Centimeters.ToString() };
        await Client.PostAsJsonAsync("/api/measurements/add", chestRequest);

        var bicepsRequest = new { bodyPart = BodyParts.Biceps.ToString(), value = 40.0, unit = HeightUnits.Centimeters.ToString() };
        await Client.PostAsJsonAsync("/api/measurements/add", bicepsRequest);

        var response = await Client.GetAsync($"/api/measurements/{userId}/getHistory?bodyPart=Chest");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MeasurementsHistoryResponse>();
        body.Should().NotBeNull();
        body!.Measurements.Should().HaveCount(1);
        body.Measurements[0].Value.Should().Be(100.0);
    }

    [Test]
    public async Task GetMeasurementsHistory_WithOtherUserId_ReturnsForbidden()
    {
        var (userId1, token1) = await RegisterUserViaEndpointAsync(
            name: "user1",
            email: "user1@example.com",
            password: "password123");

        var (userId2, _) = await RegisterUserViaEndpointAsync(
            name: "user2",
            email: "user2@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token1);

        var response = await Client.GetAsync($"/api/measurements/{userId2}/getHistory");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task GetMeasurementsHistory_WithMultipleMeasurements_ReturnsOrderedByDate()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "orderuser",
            email: "order@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var firstRequest = new { bodyPart = BodyParts.Chest.ToString(), value = 95.0, unit = HeightUnits.Centimeters.ToString() };
        await Client.PostAsJsonAsync("/api/measurements/add", firstRequest);

        var secondRequest = new { bodyPart = BodyParts.Chest.ToString(), value = 100.0, unit = HeightUnits.Centimeters.ToString() };
        await Client.PostAsJsonAsync("/api/measurements/add", secondRequest);

        var response = await Client.GetAsync($"/api/measurements/{userId}/getHistory");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MeasurementsHistoryResponse>();
        body.Should().NotBeNull();
        body!.Measurements.Should().HaveCount(2);
        body.Measurements[0].Value.Should().Be(95.0);
        body.Measurements[1].Value.Should().Be(100.0);
    }

    [Test]
    public async Task AddMeasurement_WithAliasUnit_ReturnsBadRequest()
    {
        var (_, token) = await RegisterUserViaEndpointAsync(
            name: "measurealias",
            email: "measurealias@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            bodyPart = BodyParts.Chest.ToString(),
            value = 77.7,
            unit = "cm"
        };

        var response = await Client.PostAsJsonAsync("/api/measurements/add", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task AddMeasurement_WithNumericEnumValue_ReturnsBadRequest()
    {
        var (_, token) = await RegisterUserViaEndpointAsync(
            name: "measurenumeric",
            email: "measurenumeric@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            bodyPart = 1,
            value = 77.7,
            unit = 2
        };

        var response = await Client.PostAsJsonAsync("/api/measurements/add", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed class MessageResponse
    {
        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class MeasurementsHistoryResponse
    {
        [JsonPropertyName("measurements")]
        public List<MeasurementResponse> Measurements { get; set; } = new();
    }

    private sealed class MeasurementResponse
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("bodyPart")]
        public BodyPartLookup? BodyPart { get; set; }

        [JsonPropertyName("unit")]
        public UnitLookup? Unit { get; set; }

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }
    }

    private sealed class BodyPartLookup
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class UnitLookup
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}
