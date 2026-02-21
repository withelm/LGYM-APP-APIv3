using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LgymApi.Domain.Enums;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class ContractCompatibilityTests : IntegrationTestBase
{
    [Test]
    public async Task Register_ReturnsLegacyMsgField()
    {
        var request = new
        {
            name = "contract_user",
            email = "contract_user@example.com",
            password = "password123",
            cpassword = "password123",
            isVisibleInRanking = true
        };

        var response = await Client.PostAsJsonAsync("/api/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = await ReadJsonAsync(response);
        json.RootElement.TryGetProperty("msg", out var msg).Should().BeTrue();
        msg.GetString().Should().NotBeNullOrWhiteSpace();
        // Contract guard: legacy clients expect `msg`; an accidental switch to `message` would break them.
        json.RootElement.TryGetProperty("message", out _).Should().BeFalse();
    }

    [Test]
    public async Task Login_ReturnsLegacyReqField()
    {
        await SeedUserAsync(name: "contract_login", email: "contract_login@example.com", password: "password123");

        var response = await Client.PostAsJsonAsync("/api/login", new
        {
            name = "contract_login",
            password = "password123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = await ReadJsonAsync(response);
        json.RootElement.TryGetProperty("token", out var token).Should().BeTrue();
        token.GetString().Should().NotBeNullOrWhiteSpace();

        json.RootElement.TryGetProperty("req", out var req).Should().BeTrue();
        req.TryGetProperty("_id", out _).Should().BeTrue();
        req.TryGetProperty("name", out _).Should().BeTrue();

        // Contract guard: login payload must keep `req` (legacy shape), not `user`.
        json.RootElement.TryGetProperty("user", out _).Should().BeFalse();
    }

    [Test]
    public async Task AppConfig_GetAppVersion_ReturnsExpectedJsonShape()
    {
        var admin = await SeedAdminAsync();
        SetAuthorizationHeader(admin.Id);

        var createResponse = await Client.PostAsJsonAsync($"/api/appConfig/createNewAppVersion/{admin.Id}", new
        {
            platform = Platforms.Android.ToString(),
            minRequiredVersion = "1.2.3",
            latestVersion = "2.0.0",
            forceUpdate = true,
            updateUrl = "https://example.com/app",
            releaseNotes = "contract-check"
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var getResponse = await Client.PostAsJsonAsync("/api/appConfig/getAppVersion", new
        {
            platform = Platforms.Android.ToString()
        });
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = await ReadJsonAsync(getResponse);
        json.RootElement.TryGetProperty("minRequiredVersion", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("latestVersion", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("forceUpdate", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("updateUrl", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("releaseNotes", out _).Should().BeTrue();
    }

    [Test]
    public async Task MainRecords_PossibleRecord_ReturnsExpectedJsonShape()
    {
        var user = await SeedUserAsync(name: "contract_mainrecord", email: "contract_mainrecord@example.com");
        SetAuthorizationHeader(user.Id);

        var exerciseId = await CreateExerciseViaEndpointAsync(user.Id, name: "Contract Main Record Exercise");

        var addResponse = await PostAsJsonWithApiOptionsAsync($"/api/mainRecords/{user.Id}/addNewRecord", new
        {
            exercise = exerciseId.ToString(),
            weight = 100.0,
            unit = WeightUnits.Kilograms.ToString(),
            date = DateTime.UtcNow
        });
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await Client.PostAsJsonAsync("/api/mainRecords/getRecordOrPossibleRecordInExercise", new
        {
            exerciseId = exerciseId.ToString()
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = await ReadJsonAsync(response);
        json.RootElement.TryGetProperty("weight", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("reps", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("date", out _).Should().BeTrue();

        json.RootElement.TryGetProperty("unit", out var unit).Should().BeTrue();
        unit.TryGetProperty("name", out var unitName).Should().BeTrue();
        unitName.GetString().Should().NotBeNullOrWhiteSpace();
        unit.TryGetProperty("displayName", out var unitDisplayName).Should().BeTrue();
        unitDisplayName.GetString().Should().NotBeNullOrWhiteSpace();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }
}
