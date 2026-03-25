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
        AssertLegacyMsgFieldPresent(json, "msg property must be present for backward compatibility");
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

        AssertLegacyReqFieldPresent(json, requireIdField: true, requireNameField: true);
        json.RootElement.TryGetProperty("req", out var req).Should().BeTrue();
        req.TryGetProperty("name", out var userName).Should().BeTrue();
        userName.GetString().Should().Be("contract_login");

        // Contract guard: login payload must keep `req` (legacy shape), not `user`.
        json.RootElement.TryGetProperty("user", out _).Should().BeFalse();
    }

    [Test]
    public async Task AppConfig_GetAppVersion_ReturnsExpectedJsonShape()
    {
        var admin = await SeedAdminAsync();
        SetAuthorizationHeader((Guid)admin.Id);

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
        SetAuthorizationHeader((Guid)user.Id);

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

    [Test]
    public async Task Gym_AddGym_ReturnsLegacyMsgField()
    {
        var user = await SeedUserAsync(name: "contract_gym", email: "contract_gym@example.com");
        SetAuthorizationHeader((Guid)user.Id);

        var response = await Client.PostAsJsonAsync($"/api/gym/{user.Id}/addGym", new
        {
            name = "Contract Test Gym"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = await ReadJsonAsync(response);
        AssertLegacyMsgFieldPresent(json, "addGym endpoint must return msg field for backward compatibility");
    }

    [Test]
    public async Task Gym_GetGyms_ReturnsListWithLegacyIdFields()
    {
         var user = await SeedUserAsync(name: "contract_gym_list", email: "contract_gym_list@example.com");
         SetAuthorizationHeader((Guid)user.Id);

         var gymId = await CreateGymViaEndpointAsync((Guid)user.Id, "Test Gym 1");
         await CreateGymViaEndpointAsync((Guid)user.Id, "Test Gym 2");

        var response = await Client.GetAsync($"/api/gym/{user.Id}/getGyms");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = await ReadJsonAsync(response);
        AssertJsonArrayHasLegacyIdFields(json, "each gym in list must have _id");
    }

    [Test]
    public async Task Gym_GetGym_ReturnsEntityWithLegacyIdField()
    {
         var user = await SeedUserAsync(name: "contract_gym_get", email: "contract_gym_get@example.com");
         SetAuthorizationHeader((Guid)user.Id);

         var gymId = await CreateGymViaEndpointAsync((Guid)user.Id, "Test Gym");

        var response = await Client.GetAsync($"/api/gym/{gymId}/getGym");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = await ReadJsonAsync(response);
        AssertLegacyIdFieldPresent(json, "_id property must be present for gym detail response");
    }

    [Test]
    public async Task Plan_CreatePlan_ReturnsLegacyMsgField()
    {
        var user = await SeedUserAsync(name: "contract_plan", email: "contract_plan@example.com");
        SetAuthorizationHeader((Guid)user.Id);

        var response = await Client.PostAsJsonAsync($"/api/{user.Id}/createPlan", new
        {
            name = "Contract Test Plan"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = await ReadJsonAsync(response);
        AssertLegacyMsgFieldPresent(json, "createPlan endpoint must return msg field for backward compatibility");
    }

    [Test]
    public async Task Plan_GetPlansList_ReturnsListWithLegacyIdFields()
    {
         var user = await SeedUserAsync(name: "contract_plan_list", email: "contract_plan_list@example.com");
         SetAuthorizationHeader((Guid)user.Id);

         await CreatePlanViaEndpointAsync((Guid)user.Id, "Test Plan 1");
         await CreatePlanViaEndpointAsync((Guid)user.Id, "Test Plan 2");

        var response = await Client.GetAsync($"/api/{user.Id}/getPlansList");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = await ReadJsonAsync(response);
        AssertJsonArrayHasLegacyIdFields(json, "each plan in list must have _id");
    }

    [Test]
    public async Task Exercise_AddUserExercise_ReturnsLegacyMsgField()
    {
        var user = await SeedUserAsync(name: "contract_exercise", email: "contract_exercise@example.com");
        SetAuthorizationHeader((Guid)user.Id);

        var response = await PostAsJsonWithApiOptionsAsync($"/api/exercise/{user.Id}/addUserExercise", new
        {
            name = "Contract Exercise",
            bodyPart = BodyParts.Chest.ToString(),
            description = "Test"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = await ReadJsonAsync(response);
        AssertLegacyMsgFieldPresent(json, "addUserExercise endpoint must return msg field for backward compatibility");
    }

    [Test]
    public async Task Exercise_GetAllUserExercises_ReturnsListWithLegacyIdFields()
    {
        var user = await SeedUserAsync(name: "contract_exercise_list", email: "contract_exercise_list@example.com");
        SetAuthorizationHeader((Guid)user.Id);

        await CreateExerciseViaEndpointAsync(user.Id, "Exercise A", BodyParts.Chest);
        await CreateExerciseViaEndpointAsync(user.Id, "Exercise B", BodyParts.Back);

        var response = await Client.GetAsync($"/api/exercise/{user.Id}/getAllUserExercises");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = await ReadJsonAsync(response);
        AssertJsonArrayHasLegacyIdFields(json, "each exercise in list must have _id");
    }



    [TestCase("/api/gym/{0}/addGym", "name", "Test Gym 1")]
    [TestCase("/api/{0}/createPlan", "name", "Test Plan 1")]
    public async Task PostMutationEndpoint_ReturnsLegacyMsgField(string routeTemplate, string propertyName, string propertyValue)
    {
        var user = await SeedUserAsync($"contract_{Guid.NewGuid():N}", $"contract_{Guid.NewGuid():N}@example.com");
        SetAuthorizationHeader((Guid)user.Id);

        var route = string.Format(routeTemplate, user.Id);
        var request = CreateDynamicRequest(propertyName, propertyValue);

        var response = await Client.PostAsJsonAsync(route, request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = await ReadJsonAsync(response);
        AssertLegacyMsgFieldPresent(json, $"endpoint {route} must return msg field");
    }

    [TestCase("/api/gym/{0}/getGyms")]
    [TestCase("/api/{0}/getPlansList")]
    [TestCase("/api/exercise/{0}/getAllUserExercises")]
    public async Task GetListEndpoint_ReturnsLegacyIdFields(string routeTemplate)
    {
        var user = await SeedUserAsync($"contract_list_{Guid.NewGuid():N}", $"contract_list_{Guid.NewGuid():N}@example.com");
        SetAuthorizationHeader((Guid)user.Id);

         // Seed test data for each endpoint
         if (routeTemplate.Contains("gym"))
         {
             await CreateGymViaEndpointAsync((Guid)user.Id, "Seed Gym");
         }
         else if (routeTemplate.Contains("Plan"))
         {
             await CreatePlanViaEndpointAsync((Guid)user.Id, "Seed Plan");
         }
        else if (routeTemplate.Contains("exercise"))
        {
            await CreateExerciseViaEndpointAsync(user.Id, "Seed Exercise");
        }

        var route = string.Format(routeTemplate, user.Id);

        var response = await Client.GetAsync(route);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = await ReadJsonAsync(response);
        AssertJsonArrayHasLegacyIdFields(json, $"endpoint {route} must return array items with _id");
    }

    #region Shared Assertion Helpers

    private static void AssertLegacyIdFieldPresent(JsonDocument json, string because)
    {
        json.RootElement.TryGetProperty("_id", out var id).Should().BeTrue(because);
        id.GetString().Should().NotBeNullOrWhiteSpace();
        // Contract guard: API must use `_id` (legacy MongoDB naming), not `id`
        json.RootElement.TryGetProperty("id", out _).Should().BeFalse($"response must not have 'id' field when '_id' is present (legacy compatibility)");
    }

    private static void AssertLegacyMsgFieldPresent(JsonDocument json, string because)
    {
        json.RootElement.TryGetProperty("msg", out var msg).Should().BeTrue(because);
        msg.GetString().Should().NotBeNullOrWhiteSpace();
        // Contract guard: legacy clients expect `msg`; an accidental switch to `message` would break them.
        json.RootElement.TryGetProperty("message", out _).Should().BeFalse($"response must not have 'message' field when 'msg' is present (legacy compatibility)");
    }

    private static void AssertLegacyReqFieldPresent(JsonDocument json, bool requireIdField, bool requireNameField)
    {
        json.RootElement.TryGetProperty("req", out var req).Should().BeTrue("legacy clients expect 'req' field");
        if (requireIdField)
        {
            req.TryGetProperty("_id", out var userId).Should().BeTrue("req must contain _id");
            userId.GetString().Should().NotBeNullOrWhiteSpace();
        }
        if (requireNameField)
        {
            req.TryGetProperty("name", out var userName).Should().BeTrue("req must contain name");
            userName.GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    private static void AssertJsonArrayHasLegacyIdFields(JsonDocument json, string because)
    {
        json.RootElement.ValueKind.Should().Be(JsonValueKind.Array, because);
        var array = json.RootElement.EnumerateArray().ToList();
        array.Should().NotBeEmpty(because);

        foreach (var item in array)
        {
            item.TryGetProperty("_id", out var id).Should().BeTrue($"{because} - each item must have _id");
            id.GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    private static object CreateDynamicRequest(string propertyName, object propertyValue)
    {
        return new Dictionary<string, object> { { propertyName, propertyValue } };
    }

    #endregion

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }
}
