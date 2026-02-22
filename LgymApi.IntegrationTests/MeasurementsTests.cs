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

    [Test]
    public async Task GetMeasurementsList_WithUnitConversion_ReturnsConvertedValues()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "listmeasureuser",
            email: "listmeasure@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await Client.PostAsJsonAsync("/api/measurements/add", new
        {
            bodyPart = BodyParts.Chest.ToString(),
            value = 120.0,
            unit = HeightUnits.Centimeters.ToString()
        });

        await Client.PostAsJsonAsync("/api/measurements/add", new
        {
            bodyPart = BodyParts.Chest.ToString(),
            value = 1.1,
            unit = HeightUnits.Meters.ToString()
        });

        var response = await Client.GetAsync($"/api/measurements/{userId}/list?bodyPart=Chest&unit=Centimeters");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MeasurementsHistoryResponse>();
        body.Should().NotBeNull();
        body!.Measurements.Should().HaveCount(2);
        body.Measurements[0].Value.Should().Be(110.0);
        body.Measurements[1].Value.Should().Be(120.0);
        body.Measurements[0].Unit!.Name.Should().Be(HeightUnits.Centimeters.ToString());
        body.Measurements[1].Unit!.Name.Should().Be(HeightUnits.Centimeters.ToString());
    }

    [Test]
    public async Task GetMeasurementsHistory_WithUnitConversion_ReturnsConvertedValues()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "historymeasureuser",
            email: "historymeasure@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await Client.PostAsJsonAsync("/api/measurements/add", new
        {
            bodyPart = BodyParts.Chest.ToString(),
            value = 120.0,
            unit = HeightUnits.Centimeters.ToString()
        });

        await Client.PostAsJsonAsync("/api/measurements/add", new
        {
            bodyPart = BodyParts.Chest.ToString(),
            value = 1.1,
            unit = HeightUnits.Meters.ToString()
        });

        var response = await Client.GetAsync($"/api/measurements/{userId}/getHistory?bodyPart=Chest&unit=Centimeters");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MeasurementsHistoryResponse>();
        body.Should().NotBeNull();
        body!.Measurements.Should().HaveCount(2);
        body.Measurements[0].Value.Should().Be(120.0);
        body.Measurements[1].Value.Should().Be(110.0);
        body.Measurements[0].Unit!.Name.Should().Be(HeightUnits.Centimeters.ToString());
        body.Measurements[1].Unit!.Name.Should().Be(HeightUnits.Centimeters.ToString());
    }

    [Test]
    public async Task GetMeasurementsTrend_WithMeasurements_ReturnsCorrectTrend()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "trendmeasureuser",
            email: "trendmeasure@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        await Client.PostAsJsonAsync("/api/measurements/add", new
        {
            bodyPart = BodyParts.Chest.ToString(),
            value = 100.0,
            unit = HeightUnits.Centimeters.ToString()
        });

        await Client.PostAsJsonAsync("/api/measurements/add", new
        {
            bodyPart = BodyParts.Chest.ToString(),
            value = 1.05,
            unit = HeightUnits.Meters.ToString()
        });

        var response = await Client.GetAsync($"/api/measurements/{userId}/trend?bodyPart=Chest&unit=Centimeters");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<MeasurementTrendResponse>();
        body.Should().NotBeNull();
        body!.StartValue.Should().Be(100.0);
        body.CurrentValue.Should().Be(105.0);
        body.Change.Should().Be(5.0);
        body.ChangePercentage.Should().Be(5.0);
        body.Direction.Should().Be("up");
        body.Points.Should().Be(2);
    }

    [Test]
    public async Task GetMeasurementsTrend_WithoutAuth_ReturnsUnauthorized()
    {
        var (userId, _) = await RegisterUserViaEndpointAsync(
            name: "trendauthuser",
            email: "trendauth@example.com",
            password: "password123");

        ClearAuthorizationHeader();

        var response = await Client.GetAsync($"/api/measurements/{userId}/trend?bodyPart=Chest&unit=Centimeters");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetMeasurementsTrend_WithUnknownUnit_ReturnsBadRequest()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "trendinvaliduser",
            email: "trendinvalid@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync($"/api/measurements/{userId}/trend?bodyPart=Chest&unit=Unknown");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetMeasurementsList_WithUndefinedUnitValue_ReturnsBadRequest()
    {
        var (userId, token) = await RegisterUserViaEndpointAsync(
            name: "listinvalidunituser",
            email: "listinvalidunit@example.com",
            password: "password123");

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await Client.GetAsync($"/api/measurements/{userId}/list?bodyPart=Chest&unit=999");
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

    private sealed class MeasurementTrendResponse
    {
        [JsonPropertyName("startValue")]
        public double StartValue { get; set; }

        [JsonPropertyName("currentValue")]
        public double CurrentValue { get; set; }

        [JsonPropertyName("change")]
        public double Change { get; set; }

        [JsonPropertyName("changePercentage")]
        public double ChangePercentage { get; set; }

        [JsonPropertyName("direction")]
        public string Direction { get; set; } = string.Empty;

        [JsonPropertyName("points")]
        public int Points { get; set; }
    }
}
