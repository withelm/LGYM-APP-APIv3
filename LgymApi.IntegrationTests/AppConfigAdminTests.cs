using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.IntegrationTests;

[TestFixture]
public sealed class AppConfigAdminTests : IntegrationTestBase
{
    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/appconfig/paginated", "admin", "current-permission-allow")]
    public async Task GetPaginated_ReturnsExpectedJsonShape_WithDefaultPagination()
    {
        var admin = await SeedAdminAsync();
        SetAuthorizationHeader(admin.Id);

        // Create test AppConfig
        var createResponse = await Client.PostAsJsonAsync($"/api/appConfig/createNewAppVersion/{admin.Id}", new
        {
            platform = Platforms.Android.ToString(),
            minRequiredVersion = "1.0.0",
            latestVersion = "1.1.0",
            forceUpdate = false,
            updateUrl = "https://example.com/android",
            releaseNotes = "Test Android config"
        });
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // Call paginated endpoint
        var paginatedResponse = await Client.PostAsJsonAsync("/api/appconfig/paginated", new
        {
            page = 1,
            pageSize = 20
        });
        Assert.That(paginatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var json = await ReadJsonAsync(paginatedResponse);

        // Verify pagination metadata
        Assert.That(json.RootElement.TryGetProperty("items", out var items), Is.True);
        Assert.That(items.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(items.GetArrayLength(), Is.GreaterThan(0));

        Assert.That(json.RootElement.TryGetProperty("page", out var page), Is.True);
        Assert.That(page.GetInt32(), Is.EqualTo(1));

        Assert.That(json.RootElement.TryGetProperty("pageSize", out var pageSize), Is.True);
        Assert.That(pageSize.GetInt32(), Is.EqualTo(20));

        Assert.That(json.RootElement.TryGetProperty("totalCount", out var totalCount), Is.True);
        Assert.That(totalCount.GetInt32(), Is.GreaterThan(0));

        // Verify item shape
        var firstItem = items.EnumerateArray().First();
        AssertAppConfigDetailDtoShape(firstItem);
    }

    [Test]
    public async Task GetPaginated_WithMultipleConfigs_ReturnsAllConfigs()
    {
        var admin = await SeedAdminAsync();
        SetAuthorizationHeader(admin.Id);

        // Create configs with different platforms
        await Client.PostAsJsonAsync($"/api/appConfig/createNewAppVersion/{admin.Id}", new
        {
            platform = Platforms.Android.ToString(),
            minRequiredVersion = "1.0.0",
            latestVersion = "1.1.0",
            forceUpdate = false,
            updateUrl = "https://example.com/android",
            releaseNotes = "Android config"
        });

        await Client.PostAsJsonAsync($"/api/appConfig/createNewAppVersion/{admin.Id}", new
        {
            platform = Platforms.Ios.ToString(),
            minRequiredVersion = "2.0.0",
            latestVersion = "2.1.0",
            forceUpdate = true,
            updateUrl = "https://example.com/ios",
            releaseNotes = "iOS config"
        });

        // Get all configs
        var paginatedResponse = await Client.PostAsJsonAsync("/api/appconfig/paginated", new
        {
            page = 1,
            pageSize = 20
        });
        Assert.That(paginatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var json = await ReadJsonAsync(paginatedResponse);
        Assert.That(json.RootElement.TryGetProperty("items", out var items), Is.True);

        var itemsArray = items.EnumerateArray().ToList();
        Assert.That(itemsArray, Has.Count.GreaterThanOrEqualTo(2), "at least 2 configs should exist");

        // Verify items have expected platform values
        var platforms = itemsArray.Select(item =>
        {
            Assert.That(item.TryGetProperty("platform", out var platform), Is.True);
            return platform.GetString();
        }).ToList();

        Assert.That(platforms, Does.Contain(Platforms.Android.ToString()));
        Assert.That(platforms, Does.Contain(Platforms.Ios.ToString()));
    }

    [Test]
    public async Task GetPaginated_ReturnsConfigsInCorrectOrder()
    {
        var admin = await SeedAdminAsync();
        SetAuthorizationHeader(admin.Id);

        // Create 3 AppConfigs with different timestamps
        var create1Response = await Client.PostAsJsonAsync($"/api/appConfig/createNewAppVersion/{admin.Id}", new
        {
            platform = Platforms.Android.ToString(),
            minRequiredVersion = "1.0.0",
            latestVersion = "1.1.0",
            forceUpdate = false,
            updateUrl = "https://example.com/android",
            releaseNotes = "First config"
        });
        Assert.That(create1Response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // Small delay to ensure different timestamps
        await Task.Delay(100);

        var create2Response = await Client.PostAsJsonAsync($"/api/appConfig/createNewAppVersion/{admin.Id}", new
        {
            platform = Platforms.Ios.ToString(),
            minRequiredVersion = "2.0.0",
            latestVersion = "2.1.0",
            forceUpdate = true,
            updateUrl = "https://example.com/ios",
            releaseNotes = "Second config"
        });
        Assert.That(create2Response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // Small delay to ensure different timestamps
        await Task.Delay(100);

        var create3Response = await Client.PostAsJsonAsync($"/api/appConfig/createNewAppVersion/{admin.Id}", new
        {
            platform = Platforms.Android.ToString(),
            minRequiredVersion = "3.0.0",
            latestVersion = "3.1.0",
            forceUpdate = false,
            updateUrl = "https://example.com/android2",
            releaseNotes = "Third config"
        });
        Assert.That(create3Response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // Call paginated endpoint
        var paginatedResponse = await Client.PostAsJsonAsync("/api/appconfig/paginated", new
        {
            page = 1,
            pageSize = 20
        });
        Assert.That(paginatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var json = await ReadJsonAsync(paginatedResponse);
        Assert.That(json.RootElement.TryGetProperty("items", out var items), Is.True);

        var itemsArray = items.EnumerateArray().ToList();
        Assert.That(itemsArray.Count, Is.GreaterThanOrEqualTo(3), "at least 3 configs should exist");

        // Verify pagination metadata
        Assert.That(json.RootElement.TryGetProperty("page", out var page), Is.True);
        Assert.That(page.GetInt32(), Is.EqualTo(1));

        Assert.That(json.RootElement.TryGetProperty("pageSize", out var pageSize), Is.True);
        Assert.That(pageSize.GetInt32(), Is.EqualTo(20));

        Assert.That(json.RootElement.TryGetProperty("totalCount", out var totalCount), Is.True);
        Assert.That(totalCount.GetInt32(), Is.GreaterThanOrEqualTo(3));

        // Verify items have expected properties (createdAt values should exist and be comparable)
        var createdAtValues = itemsArray.Select(item =>
        {
            Assert.That(item.TryGetProperty("createdAt", out var createdAt), Is.True);
            var createdAtString = createdAt.GetString();
            Assert.That(string.IsNullOrWhiteSpace(createdAtString), Is.False);
            return createdAtString;
        }).ToList();

        Assert.That(createdAtValues, Has.Exactly(itemsArray.Count).Items);
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("GET", "/api/appconfig/{id}", "admin", "current-permission-allow")]
    public async Task GetById_ReturnsExpectedJsonShape()
    {
        var admin = await SeedAdminAsync();
        SetAuthorizationHeader(admin.Id);

        // Create test AppConfig
        var createResponse = await Client.PostAsJsonAsync($"/api/appConfig/createNewAppVersion/{admin.Id}", new
        {
            platform = Platforms.Android.ToString(),
            minRequiredVersion = "1.0.0",
            latestVersion = "1.1.0",
            forceUpdate = true,
            updateUrl = "https://example.com/android",
            releaseNotes = "Detailed test config"
        });
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // Fetch the created config via paginated endpoint to get its ID
        var paginatedResponse = await Client.PostAsJsonAsync("/api/appconfig/paginated", new
        {
            page = 1,
            pageSize = 20
        });
        Assert.That(paginatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var paginatedJson = await ReadJsonAsync(paginatedResponse);
        Assert.That(paginatedJson.RootElement.TryGetProperty("items", out var items), Is.True);
        var configId = items.EnumerateArray().First().GetProperty("id").GetString();
        Assert.That(string.IsNullOrWhiteSpace(configId), Is.False);

        // Get by ID
        var getResponse = await Client.GetAsync($"/api/appconfig/{configId}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var json = await ReadJsonAsync(getResponse);
        AssertAppConfigDetailDtoShape(json.RootElement);

        // Verify specific values match creation
        Assert.That(json.RootElement.TryGetProperty("platform", out var platform), Is.True);
        Assert.That(platform.GetString(), Is.EqualTo(Platforms.Android.ToString()));

        Assert.That(json.RootElement.TryGetProperty("minRequiredVersion", out var minVersion), Is.True);
        Assert.That(minVersion.GetString(), Is.EqualTo("1.0.0"));

        Assert.That(json.RootElement.TryGetProperty("forceUpdate", out var forceUpdate), Is.True);
        Assert.That(forceUpdate.GetBoolean(), Is.True);
    }

    [Test]
    public async Task GetById_WithInvalidId_Returns404()
    {
        var admin = await SeedAdminAsync();
        SetAuthorizationHeader(admin.Id);

        var randomId = Id<AppConfig>.New().ToString();
        var getResponse = await Client.GetAsync($"/api/appconfig/{randomId}");

        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetById_Unauthenticated_Returns401()
    {
        ClearAuthorizationHeader();

        var randomId = Id<AppConfig>.New().ToString();
        var getResponse = await Client.GetAsync($"/api/appconfig/{randomId}");

        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/appconfig/{id}/update", "admin", "current-permission-allow")]
    public async Task Update_ModifiesAppConfigAndPersistsChanges()
    {
        var admin = await SeedAdminAsync();
        SetAuthorizationHeader(admin.Id);

        // Create initial AppConfig
        var createResponse = await Client.PostAsJsonAsync($"/api/appConfig/createNewAppVersion/{admin.Id}", new
        {
            platform = Platforms.Android.ToString(),
            minRequiredVersion = "1.0.0",
            latestVersion = "1.1.0",
            forceUpdate = false,
            updateUrl = "https://example.com/android",
            releaseNotes = "Initial notes"
        });
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // Fetch the created config via paginated endpoint to get its ID
        var paginatedResponse = await Client.PostAsJsonAsync("/api/appconfig/paginated", new
        {
            page = 1,
            pageSize = 20
        });
        Assert.That(paginatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var paginatedJson = await ReadJsonAsync(paginatedResponse);
        Assert.That(paginatedJson.RootElement.TryGetProperty("items", out var items), Is.True);
        var configId = items.EnumerateArray().First().GetProperty("id").GetString();

        // Update AppConfig
        var updateResponse = await Client.PostAsJsonAsync($"/api/appconfig/{configId}/update", new
        {
            platform = Platforms.Android.ToString(),
            minRequiredVersion = "2.0.0",
            latestVersion = "2.5.0",
            forceUpdate = true,
            updateUrl = "https://updated.example.com/android",
            releaseNotes = "Updated release notes"
        });
        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Verify changes persisted via GetById
        var getResponse = await Client.GetAsync($"/api/appconfig/{configId}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var json = await ReadJsonAsync(getResponse);
        Assert.That(json.RootElement.TryGetProperty("minRequiredVersion", out var minVersion), Is.True);
        Assert.That(minVersion.GetString(), Is.EqualTo("2.0.0"));

        Assert.That(json.RootElement.TryGetProperty("latestVersion", out var latestVersion), Is.True);
        Assert.That(latestVersion.GetString(), Is.EqualTo("2.5.0"));

        Assert.That(json.RootElement.TryGetProperty("forceUpdate", out var forceUpdate), Is.True);
        Assert.That(forceUpdate.GetBoolean(), Is.True);

        Assert.That(json.RootElement.TryGetProperty("updateUrl", out var updateUrl), Is.True);
        Assert.That(updateUrl.GetString(), Is.EqualTo("https://updated.example.com/android"));

        Assert.That(json.RootElement.TryGetProperty("releaseNotes", out var releaseNotes), Is.True);
        Assert.That(releaseNotes.GetString(), Is.EqualTo("Updated release notes"));

        // Verify updatedAt is set
        Assert.That(json.RootElement.TryGetProperty("updatedAt", out var updatedAt), Is.True);
        Assert.That(updatedAt.ValueKind, Is.Not.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task Update_Unauthenticated_Returns401()
    {
        ClearAuthorizationHeader();

        var randomId = Id<AppConfig>.New().ToString();
        var updateResponse = await Client.PostAsJsonAsync($"/api/appconfig/{randomId}/update", new
        {
            platform = Platforms.Android.ToString(),
            minRequiredVersion = "1.0.0",
            latestVersion = "1.1.0",
            forceUpdate = false,
            updateUrl = "https://example.com",
            releaseNotes = "test"
        });

        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    [LgymApi.IntegrationTests.Authorization.AuthorizationEvidence("POST", "/api/appconfig/{id}/delete", "admin", "current-permission-allow")]
    public async Task Delete_RemovesAppConfig()
    {
        var admin = await SeedAdminAsync();
        SetAuthorizationHeader(admin.Id);

        // Create AppConfig
        var createResponse = await Client.PostAsJsonAsync($"/api/appConfig/createNewAppVersion/{admin.Id}", new
        {
            platform = Platforms.Android.ToString(),
            minRequiredVersion = "1.0.0",
            latestVersion = "1.1.0",
            forceUpdate = false,
            updateUrl = "https://example.com/android",
            releaseNotes = "To be deleted"
        });
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // Fetch the created config via paginated endpoint to get its ID
        var paginatedResponse = await Client.PostAsJsonAsync("/api/appconfig/paginated", new
        {
            page = 1,
            pageSize = 20
        });
        Assert.That(paginatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var paginatedJson = await ReadJsonAsync(paginatedResponse);
        Assert.That(paginatedJson.RootElement.TryGetProperty("items", out var items), Is.True);
        var configId = items.EnumerateArray().First().GetProperty("id").GetString();

        // Verify config exists
        var getBeforeDelete = await Client.GetAsync($"/api/appconfig/{configId}");
        Assert.That(getBeforeDelete.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Delete AppConfig
        var deleteResponse = await Client.PostAsJsonAsync($"/api/appconfig/{configId}/delete", new { });
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Verify config is gone
        var getAfterDelete = await Client.GetAsync($"/api/appconfig/{configId}");
        Assert.That(getAfterDelete.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Delete_Unauthenticated_Returns401()
    {
        ClearAuthorizationHeader();

        var randomId = Id<AppConfig>.New().ToString();
        var deleteResponse = await Client.PostAsJsonAsync($"/api/appconfig/{randomId}/delete", new { });

        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Paginated_Unauthenticated_Returns401()
    {
        ClearAuthorizationHeader();

        var paginatedResponse = await Client.PostAsJsonAsync("/api/appconfig/paginated", new
        {
            page = 1,
            pageSize = 20
        });

        Assert.That(paginatedResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    #region Helper Methods

    private static void AssertAppConfigDetailDtoShape(JsonElement element)
    {
        Assert.That(element.TryGetProperty("id", out var id), Is.True, "AppConfigDetailDto must have id field");
        Assert.That(string.IsNullOrWhiteSpace(id.GetString()), Is.False);

        Assert.That(element.TryGetProperty("platform", out var platform), Is.True, "AppConfigDetailDto must have platform field");
        Assert.That(string.IsNullOrWhiteSpace(platform.GetString()), Is.False);

        Assert.That(element.TryGetProperty("minRequiredVersion", out var minRequiredVersion), Is.True, "AppConfigDetailDto must have minRequiredVersion field");
        Assert.That(string.IsNullOrWhiteSpace(minRequiredVersion.GetString()), Is.False);

        Assert.That(element.TryGetProperty("latestVersion", out var latestVersion), Is.True, "AppConfigDetailDto must have latestVersion field");
        Assert.That(string.IsNullOrWhiteSpace(latestVersion.GetString()), Is.False);

        Assert.That(element.TryGetProperty("forceUpdate", out var forceUpdate), Is.True, "AppConfigDetailDto must have forceUpdate field");
        Assert.That(forceUpdate.ValueKind, Is.AnyOf(JsonValueKind.True, JsonValueKind.False));

        Assert.That(element.TryGetProperty("updateUrl", out var updateUrl), Is.True, "AppConfigDetailDto must have updateUrl field");
        Assert.That(string.IsNullOrWhiteSpace(updateUrl.GetString()), Is.False);

        Assert.That(element.TryGetProperty("releaseNotes", out _), Is.True, "AppConfigDetailDto must have releaseNotes field");

        Assert.That(element.TryGetProperty("createdAt", out var createdAt), Is.True, "AppConfigDetailDto must have createdAt field");
        Assert.That(string.IsNullOrWhiteSpace(createdAt.GetString()), Is.False);

        Assert.That(element.TryGetProperty("updatedAt", out _), Is.True, "AppConfigDetailDto must have updatedAt field");
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }

    #endregion
}
