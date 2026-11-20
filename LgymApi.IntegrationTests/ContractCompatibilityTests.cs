using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;

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

        // Set idempotency key for registration endpoint (required by T9 middleware)
        SetIdempotencyKey("test-contract-register-legacy-msg");

        var response = await Client.PostAsJsonAsync("/api/register", request);

        // Clear idempotency key after request
        ClearIdempotencyKey();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

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

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var json = await ReadJsonAsync(response);
        Assert.That(json.RootElement.TryGetProperty("token", out var token), Is.True);
        Assert.That(string.IsNullOrWhiteSpace(token.GetString()), Is.False);

        AssertLegacyReqFieldPresent(json, requireIdField: true, requireNameField: true);
        Assert.That(json.RootElement.TryGetProperty("req", out var req), Is.True);
        Assert.That(req.TryGetProperty("name", out var userName), Is.True);
        Assert.That(userName.GetString(), Is.EqualTo("contract_login"));

        // Contract guard: login payload must keep `req` (legacy shape), not `user`.
        Assert.That(json.RootElement.TryGetProperty("user", out _), Is.False);
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
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var getResponse = await Client.PostAsJsonAsync("/api/appConfig/getAppVersion", new
        {
            platform = Platforms.Android.ToString()
        });
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var json = await ReadJsonAsync(getResponse);
        Assert.That(json.RootElement.TryGetProperty("minRequiredVersion", out _), Is.True);
        Assert.That(json.RootElement.TryGetProperty("latestVersion", out _), Is.True);
        Assert.That(json.RootElement.TryGetProperty("forceUpdate", out _), Is.True);
        Assert.That(json.RootElement.TryGetProperty("updateUrl", out _), Is.True);
        Assert.That(json.RootElement.TryGetProperty("releaseNotes", out _), Is.True);
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
        Assert.That(addResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var response = await Client.PostAsJsonAsync("/api/mainRecords/getRecordOrPossibleRecordInExercise", new
        {
            exerciseId = exerciseId.ToString()
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var json = await ReadJsonAsync(response);
        Assert.That(json.RootElement.TryGetProperty("weight", out _), Is.True);
        Assert.That(json.RootElement.TryGetProperty("reps", out _), Is.True);
        Assert.That(json.RootElement.TryGetProperty("date", out _), Is.True);

        Assert.That(json.RootElement.TryGetProperty("unit", out var unit), Is.True);
        Assert.That(unit.TryGetProperty("name", out var unitName), Is.True);
        Assert.That(string.IsNullOrWhiteSpace(unitName.GetString()), Is.False);
        Assert.That(unit.TryGetProperty("displayName", out var unitDisplayName), Is.True);
        Assert.That(string.IsNullOrWhiteSpace(unitDisplayName.GetString()), Is.False);
    }

    [Test]
    public async Task Measurements_LegacyRoutes_PreserveMessagesAndObjectEnvelopes()
    {
        var user = await SeedUserAsync(name: "contract_measurements", email: "contract_measurements@example.com");
        SetAuthorizationHeader(user.Id);

        var addResponse = await Client.PostAsJsonAsync("/api/measurements/add", new
        {
            bodyPart = BodyParts.BodyWeight.ToString(),
            unit = MeasurementUnits.Kilograms.ToString(),
            value = 80.5
        });
        Assert.That(addResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var addJson = await ReadJsonAsync(addResponse))
        {
            AssertLegacyMsgFieldPresent(addJson, "measurement add must return the legacy msg envelope");
            Assert.That(addJson.RootElement.GetProperty("msg").GetString(), Is.EqualTo("Created"));
        }

        var bulkResponse = await Client.PostAsJsonAsync("/api/measurements/add-bulk", new
        {
            measurements = new[]
            {
                new
                {
                    bodyPart = BodyParts.BodyWeight.ToString(),
                    unit = MeasurementUnits.Kilograms.ToString(),
                    value = 81.0
                },
                new
                {
                    bodyPart = BodyParts.Waist.ToString(),
                    unit = MeasurementUnits.Centimeters.ToString(),
                    value = 90.0
                }
            }
        });
        Assert.That(bulkResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var bulkJson = await ReadJsonAsync(bulkResponse))
        {
            AssertLegacyMsgFieldPresent(bulkJson, "measurement bulk add must return the legacy msg envelope");
            Assert.That(bulkJson.RootElement.GetProperty("msg").GetString(), Is.EqualTo("Created"));
        }

        foreach (var route in new[]
                 {
                     $"/api/measurements/{user.Id}/getHistory?bodyPart=BodyWeight&unit=Kilograms",
                     $"/api/measurements/{user.Id}/list?bodyPart=BodyWeight&unit=Kilograms"
                 })
        {
            var response = await Client.GetAsync(route);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            using var json = await ReadJsonAsync(response);
            Assert.That(json.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object));
            var measurements = json.RootElement.GetProperty("measurements");
            Assert.That(measurements.ValueKind, Is.EqualTo(JsonValueKind.Array));
            var measurementItems = measurements.EnumerateArray().ToList();
            Assert.That(measurementItems, Has.Exactly(2).Items);
            Assert.That((measurementItems).All(measurement =>
                measurement.GetProperty("user").GetString() == user.Id.ToString()
                && measurement.GetProperty("bodyPart").GetProperty("id").GetString() == BodyParts.BodyWeight.ToString()
                && measurement.GetProperty("unit").GetProperty("id").GetString() == MeasurementUnits.Kilograms.ToString()), Is.True);
            Assert.That(measurementItems.Select(measurement => measurement.GetProperty("value").GetDouble()), Is.EquivalentTo(new[] { 80.5, 81.0 }));
            AssertEnumLookup(measurementItems[0].GetProperty("bodyPart"), BodyParts.BodyWeight.ToString());
            AssertEnumLookup(measurementItems[0].GetProperty("unit"), MeasurementUnits.Kilograms.ToString());
        }

        var trendResponse = await Client.GetAsync($"/api/measurements/{user.Id}/trend?bodyPart=BodyWeight&unit=Kilograms");
        Assert.That(trendResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var trendJson = await ReadJsonAsync(trendResponse))
        {
            Assert.That(trendJson.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object));
            AssertEnumLookup(trendJson.RootElement.GetProperty("bodyPart"), BodyParts.BodyWeight.ToString());
            AssertEnumLookup(trendJson.RootElement.GetProperty("unit"), MeasurementUnits.Kilograms.ToString());
            Assert.That(trendJson.RootElement.GetProperty("direction").GetString(), Is.EqualTo("up"));
            Assert.That(trendJson.RootElement.GetProperty("points").GetInt32(), Is.EqualTo(2));
        }

        var trendsResponse = await Client.GetAsync($"/api/measurements/{user.Id}/trends");
        Assert.That(trendsResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var trendsJson = await ReadJsonAsync(trendsResponse);
        Assert.That(trendsJson.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object));
        var trends = trendsJson.RootElement.GetProperty("trends");
        Assert.That(trends.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That((trends.EnumerateArray().ToList()).Any(trend => trend.GetProperty("bodyPart").GetProperty("id").GetString() == BodyParts.BodyWeight.ToString()), Is.True);
    }

    [Test]
    public async Task MainRecords_LegacyRoutes_PreserveMessagesIdsAndEnumLookups()
    {
        var user = await SeedUserAsync(name: "contract_main_records", email: "contract_main_records@example.com");
        SetAuthorizationHeader(user.Id);
        var exerciseId = await CreateExerciseViaEndpointAsync(user.Id, "Contract Main Record");

        var addResponse = await PostAsJsonWithApiOptionsAsync($"/api/mainRecords/{user.Id}/addNewRecord", new
        {
            exercise = exerciseId.ToString(),
            weight = 100.0,
            unit = WeightUnits.Kilograms.ToString(),
            date = DateTime.UtcNow
        });
        Assert.That(addResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var addJson = await ReadJsonAsync(addResponse))
        {
            AssertLegacyMsgFieldPresent(addJson, "main-record add must return the legacy msg envelope");
            Assert.That(addJson.RootElement.GetProperty("msg").GetString(), Is.EqualTo("Created"));
        }

        var historyResponse = await Client.GetAsync($"/api/mainRecords/{user.Id}/getMainRecordsHistory");
        Assert.That(historyResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        string recordId;
        using (var historyJson = await ReadJsonAsync(historyResponse))
        {
            Assert.That(historyJson.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            var record = historyJson.RootElement.EnumerateArray().Single();
            recordId = record.GetProperty("_id").GetString()!;
            Assert.That(Id<LgymApi.Domain.Entities.MainRecord>.TryParse(recordId, out _), Is.True);
            Assert.That(record.GetProperty("exercise").GetString(), Is.EqualTo(exerciseId.ToString()));
            AssertEnumLookup(record.GetProperty("unit"), WeightUnits.Kilograms.ToString());
        }

        var lastResponse = await Client.GetAsync($"/api/mainRecords/{user.Id}/getLastMainRecords");
        Assert.That(lastResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var lastJson = await ReadJsonAsync(lastResponse))
        {
            var record = lastJson.RootElement.EnumerateArray().Single();
            Assert.That(record.GetProperty("_id").GetString(), Is.EqualTo(recordId));
            Assert.That(record.GetProperty("exerciseDetails").GetProperty("_id").GetString(), Is.EqualTo(exerciseId.ToString()));
        }

        var possibleResponse = await Client.PostAsJsonAsync("/api/mainRecords/getRecordOrPossibleRecordInExercise", new { exerciseId = exerciseId.ToString() });
        Assert.That(possibleResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var possibleJson = await ReadJsonAsync(possibleResponse))
        {
            Assert.That(possibleJson.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object));
            Assert.That(possibleJson.RootElement.GetProperty("weight").GetDouble(), Is.EqualTo(100.0));
            Assert.That(possibleJson.RootElement.GetProperty("reps").GetDouble(), Is.EqualTo(1));
            AssertEnumLookup(possibleJson.RootElement.GetProperty("unit"), WeightUnits.Kilograms.ToString());
        }

        var updateResponse = await PostAsJsonWithApiOptionsAsync($"/api/mainRecords/{user.Id}/updateMainRecords", new
        {
            _id = recordId,
            exercise = exerciseId.ToString(),
            weight = 105.0,
            unit = WeightUnits.Kilograms.ToString(),
            date = DateTime.UtcNow
        });
        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var updateJson = await ReadJsonAsync(updateResponse))
        {
            AssertLegacyMsgFieldPresent(updateJson, "main-record update must return the legacy msg envelope");
            Assert.That(updateJson.RootElement.GetProperty("msg").GetString(), Is.EqualTo("Updated"));
        }

        var deleteResponse = await Client.GetAsync($"/api/mainRecords/{recordId}/deleteMainRecord");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var deleteJson = await ReadJsonAsync(deleteResponse);
        AssertLegacyMsgFieldPresent(deleteJson, "legacy main-record delete must retain its GET route and msg envelope");
    }

    [Test]
    public async Task EloAndRanking_LegacyRoutes_PreserveArrayAndMessageShapes()
    {
        var visibleUser = await SeedUserAsync(name: "contract_visible", email: "contract_visible@example.com", elo: 1250);
        await SeedUserAsync(name: "contract_hidden", email: "contract_hidden@example.com", elo: 3000, isVisibleInRanking: false);
        await SeedUserAsync(name: "contract_tester", email: "contract_tester@example.com", elo: 2900, isTester: true);
        await SeedUserAsync(name: "contract_deleted", email: "contract_deleted@example.com", elo: 2800, isDeleted: true);
        SetAuthorizationHeader(visibleUser.Id);

        var chartResponse = await Client.GetAsync($"/api/eloRegistry/{visibleUser.Id}/getEloRegistryChart");
        Assert.That(chartResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var chartJson = await ReadJsonAsync(chartResponse))
        {
            Assert.That(chartJson.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            var entry = chartJson.RootElement.EnumerateArray().Single();
            Assert.That(Id<LgymApi.Domain.Entities.EloRegistry>.TryParse(entry.GetProperty("_id").GetString(), out _), Is.True);
            Assert.That(entry.GetProperty("value").GetInt32(), Is.EqualTo(1250));
            Assert.That(string.IsNullOrWhiteSpace(entry.GetProperty("date").GetString()), Is.False);
        }

        var eloResponse = await Client.GetAsync($"/api/userInfo/{visibleUser.Id}/getUserEloPoints");
        Assert.That(eloResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var eloJson = await ReadJsonAsync(eloResponse))
        {
            Assert.That(eloJson.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object));
            Assert.That(eloJson.RootElement.GetProperty("elo").GetInt32(), Is.EqualTo(1250));
            Assert.That(eloJson.RootElement.TryGetProperty("req", out _), Is.False, "the legacy user ELO route returns a dedicated object, not the login envelope");
        }

        var rankingResponse = await Client.GetAsync("/api/getUsersRanking");
        Assert.That(rankingResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var rankingJson = await ReadJsonAsync(rankingResponse))
        {
            Assert.That(rankingJson.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            var ranking = rankingJson.RootElement.EnumerateArray().Single();
            Assert.That(ranking.GetProperty("name").GetString(), Is.EqualTo("contract_visible"));
            Assert.That(ranking.GetProperty("elo").GetInt32(), Is.EqualTo(1250));
            Assert.That(ranking.TryGetProperty("avatar", out _), Is.False, "null avatar values are omitted by the established JSON contract");
            Assert.That(string.IsNullOrWhiteSpace(ranking.GetProperty("profileRank").GetString()), Is.False);
            Assert.That(ranking.TryGetProperty("_id", out _), Is.False, "ranking uses its established anonymous entry shape");
        }

        var visibilityResponse = await Client.PostAsJsonAsync("/api/changeVisibilityInRanking", new { isVisibleInRanking = false });
        Assert.That(visibilityResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var visibilityJson = await ReadJsonAsync(visibilityResponse);
        AssertLegacyMsgFieldPresent(visibilityJson, "ranking visibility mutation must return the legacy msg envelope");
        Assert.That(visibilityJson.RootElement.GetProperty("msg").GetString(), Is.EqualTo("Updated"));
    }

    [Test]
    public async Task Gym_AddGym_ReturnsLegacyMsgField()
    {
        var user = await SeedUserAsync(name: "contract_gym", email: "contract_gym@example.com");
        SetAuthorizationHeader(user.Id);

        var response = await Client.PostAsJsonAsync($"/api/gym/{user.Id}/addGym", new
        {
            name = "Contract Test Gym"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var json = await ReadJsonAsync(response);
        AssertLegacyMsgFieldPresent(json, "addGym endpoint must return msg field for backward compatibility");
    }

    [Test]
    public async Task Gym_GetGyms_ReturnsListWithLegacyIdFields()
    {
        var user = await SeedUserAsync(name: "contract_gym_list", email: "contract_gym_list@example.com");
        SetAuthorizationHeader(user.Id);

        var gymId = await CreateGymViaEndpointAsync(user.Id, "Test Gym 1");
        await CreateGymViaEndpointAsync(user.Id, "Test Gym 2");

        var response = await Client.GetAsync($"/api/gym/{user.Id}/getGyms");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var json = await ReadJsonAsync(response);
        AssertJsonArrayHasLegacyIdFields(json, "each gym in list must have _id");
    }

    [Test]
    public async Task Gym_GetGym_ReturnsEntityWithLegacyIdField()
    {
        var user = await SeedUserAsync(name: "contract_gym_get", email: "contract_gym_get@example.com");
        SetAuthorizationHeader(user.Id);

        var gymId = await CreateGymViaEndpointAsync(user.Id, "Test Gym");

        var response = await Client.GetAsync($"/api/gym/{gymId}/getGym");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var json = await ReadJsonAsync(response);
        AssertLegacyIdFieldPresent(json, "_id property must be present for gym detail response");
    }

    [Test]
    public async Task Plan_CreatePlan_ReturnsLegacyMsgField()
    {
        var user = await SeedUserAsync(name: "contract_plan", email: "contract_plan@example.com");
        SetAuthorizationHeader(user.Id);

        var response = await Client.PostAsJsonAsync($"/api/{user.Id}/createPlan", new
        {
            name = "Contract Test Plan"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var json = await ReadJsonAsync(response);
        AssertLegacyMsgFieldPresent(json, "createPlan endpoint must return msg field for backward compatibility");
    }

    [Test]
    public async Task Plan_GetPlansList_ReturnsListWithLegacyIdFields()
    {
        var user = await SeedUserAsync(name: "contract_plan_list", email: "contract_plan_list@example.com");
        SetAuthorizationHeader(user.Id);

        await CreatePlanViaEndpointAsync(user.Id, "Test Plan 1");
        await CreatePlanViaEndpointAsync(user.Id, "Test Plan 2");

        var response = await Client.GetAsync($"/api/{user.Id}/getPlansList");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var json = await ReadJsonAsync(response);
        AssertJsonArrayHasLegacyIdFields(json, "each plan in list must have _id");
    }

    [Test]
    public async Task Plan_UpdatePlan_ReturnsLegacyMsgField()
    {
        var user = await SeedUserAsync(name: "contract_plan_update", email: "contract_plan_update@example.com");
        var planId = await CreatePlanViaEndpointAsync(user.Id, "Contract Plan");

        var response = await PostAsJsonWithApiOptionsAsync($"/api/{user.Id}/updatePlan", new
        {
            _id = planId.ToString(),
            name = "Updated Contract Plan"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var json = await ReadJsonAsync(response);
        AssertLegacyMsgFieldPresent(json, "updatePlan endpoint must return msg field for backward compatibility");
        Assert.That(json.RootElement.GetProperty("msg").GetString(), Is.EqualTo("Updated"));
    }

    [Test]
    public async Task Plan_GetPlanConfig_ReturnsLegacyPlanFormShape()
    {
        var user = await SeedUserAsync(name: "contract_plan_config", email: "contract_plan_config@example.com");
        var planId = await CreatePlanViaEndpointAsync(user.Id, "Configured Plan");

        var response = await Client.GetAsync($"/api/{user.Id}/getPlanConfig");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var json = await ReadJsonAsync(response);
        AssertLegacyIdFieldPresent(json, "getPlanConfig must return the legacy _id field");
        Assert.That(json.RootElement.GetProperty("_id").GetString(), Is.EqualTo(planId.ToString()));
        Assert.That(json.RootElement.GetProperty("name").GetString(), Is.EqualTo("Configured Plan"));
        Assert.That(json.RootElement.GetProperty("isActive").GetBoolean(), Is.True);
    }

    [Test]
    public async Task Plan_CheckIsUserHavePlan_WithPlanWithoutDays_ReturnsBooleanFalsePayload()
    {
        var user = await SeedUserAsync(name: "contract_plan_check", email: "contract_plan_check@example.com");
        await CreatePlanViaEndpointAsync(user.Id, "Existing Plan");

        var response = await Client.GetAsync($"/api/{user.Id}/checkIsUserHavePlan");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var json = await ReadJsonAsync(response);
        Assert.That(json.RootElement.ValueKind, Is.EqualTo(JsonValueKind.False));
    }

    [Test]
    public async Task Plan_SetNewActivePlan_ReturnsLegacyMsgField()
    {
        var user = await SeedUserAsync(name: "contract_plan_active", email: "contract_plan_active@example.com");
        await CreatePlanViaEndpointAsync(user.Id, "First Plan");
        var planId = await CreatePlanViaEndpointAsync(user.Id, "Second Plan");

        var response = await PostAsJsonWithApiOptionsAsync($"/api/{user.Id}/setNewActivePlan", new
        {
            _id = planId.ToString()
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var json = await ReadJsonAsync(response);
        AssertLegacyMsgFieldPresent(json, "setNewActivePlan endpoint must return msg field for backward compatibility");
        Assert.That(json.RootElement.GetProperty("msg").GetString(), Is.EqualTo("Updated"));
    }

    [Test]
    public async Task Plan_CopyPlan_ReturnsPlanDtoShape()
    {
        var sourceUser = await SeedUserAsync(name: "contract_plan_copy_source", email: "contract_plan_copy_source@example.com");
        var sourcePlanId = await CreatePlanViaEndpointAsync(sourceUser.Id, "Copy Source Plan");

        var shareResponse = await Client.PostAsync($"/api/{sourcePlanId}/share", null);
        Assert.That(shareResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var shareJson = await ReadJsonAsync(shareResponse);
        var shareCode = shareJson.RootElement.GetProperty("shareCode").GetString();

        var destinationUser = await SeedUserAsync(name: "contract_plan_copy_destination", email: "contract_plan_copy_destination@example.com");
        SetAuthorizationHeader(destinationUser.Id);
        var response = await Client.PostAsJsonAsync("/api/copy", new { shareCode });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using var json = await ReadJsonAsync(response);
        Assert.That(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("id").GetString()), Is.False);
        Assert.That(json.RootElement.TryGetProperty("_id", out _), Is.False, "copyPlan returns PlanDto, which uses id rather than legacy _id");
        Assert.That(json.RootElement.GetProperty("name").GetString(), Is.EqualTo("Copy Source Plan"));
        Assert.That(json.RootElement.GetProperty("isActive").GetBoolean(), Is.True);
        Assert.That(json.RootElement.GetProperty("userId").GetString(), Is.EqualTo(destinationUser.Id.ToString()));
    }

    [Test]
    public async Task Plan_GenerateShareCode_ReturnsShareCodeShape()
    {
        var user = await SeedUserAsync(name: "contract_plan_share", email: "contract_plan_share@example.com");
        var planId = await CreatePlanViaEndpointAsync(user.Id, "Share Plan");

        var response = await Client.PostAsync($"/api/{planId}/share", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var json = await ReadJsonAsync(response);
        Assert.That(json.RootElement.TryGetProperty("shareCode", out var shareCode), Is.True, "share endpoint must return shareCode");
        Assert.That(shareCode.GetString(), Has.Length.EqualTo(10));
        Assert.That(json.RootElement.TryGetProperty("code", out _), Is.False, "share endpoint must retain the shareCode property name");
    }

    [Test]
    public async Task Plan_DeletePlan_ReturnsLegacyMsgField()
    {
        var user = await SeedUserAsync(name: "contract_plan_delete", email: "contract_plan_delete@example.com");
        var planId = await CreatePlanViaEndpointAsync(user.Id, "Delete Plan");

        var response = await Client.PostAsync($"/api/{planId}/deletePlan", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var json = await ReadJsonAsync(response);
        AssertLegacyMsgFieldPresent(json, "deletePlan endpoint must return msg field for backward compatibility");
        Assert.That(json.RootElement.GetProperty("msg").GetString(), Is.EqualTo("Deleted."));
    }

    [Test]
    public async Task Exercise_AddUserExercise_ReturnsLegacyMsgField()
    {
        var user = await SeedUserAsync(name: "contract_exercise", email: "contract_exercise@example.com");
        SetAuthorizationHeader(user.Id);

        var response = await PostAsJsonWithApiOptionsAsync($"/api/exercise/{user.Id}/addUserExercise", new
        {
            name = "Contract Exercise",
            bodyPart = BodyParts.Chest.ToString(),
            description = "Test"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var json = await ReadJsonAsync(response);
        AssertLegacyMsgFieldPresent(json, "addUserExercise endpoint must return msg field for backward compatibility");
    }

    [Test]
    public async Task Exercise_GetAllUserExercises_ReturnsListWithLegacyIdFields()
    {
        var user = await SeedUserAsync(name: "contract_exercise_list", email: "contract_exercise_list@example.com");
        SetAuthorizationHeader(user.Id);

        await CreateExerciseViaEndpointAsync(user.Id, "Exercise A", BodyParts.Chest);
        await CreateExerciseViaEndpointAsync(user.Id, "Exercise B", BodyParts.Back);

        var response = await Client.GetAsync($"/api/exercise/{user.Id}/getAllUserExercises");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var json = await ReadJsonAsync(response);
        AssertJsonArrayHasLegacyIdFields(json, "each exercise in list must have _id");
    }



    [TestCase("/api/gym/{0}/addGym", "name", "Test Gym 1")]
    [TestCase("/api/{0}/createPlan", "name", "Test Plan 1")]
    public async Task PostMutationEndpoint_ReturnsLegacyMsgField(string routeTemplate, string propertyName, string propertyValue)
    {
        var user = await SeedUserAsync($"contract_{Id<ContractCompatibilityTests>.New()}", $"contract_{Id<ContractCompatibilityTests>.New()}@example.com");
        SetAuthorizationHeader(user.Id);

        var route = string.Format(routeTemplate, user.Id);
        var request = CreateDynamicRequest(propertyName, propertyValue);

        var response = await Client.PostAsJsonAsync(route, request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var json = await ReadJsonAsync(response);
        AssertLegacyMsgFieldPresent(json, $"endpoint {route} must return msg field");
    }

    [TestCase("/api/gym/{0}/getGyms")]
    [TestCase("/api/{0}/getPlansList")]
    [TestCase("/api/exercise/{0}/getAllUserExercises")]
    public async Task GetListEndpoint_ReturnsLegacyIdFields(string routeTemplate)
    {
        var user = await SeedUserAsync($"contract_list_{Id<ContractCompatibilityTests>.New()}", $"contract_list_{Id<ContractCompatibilityTests>.New()}@example.com");
        SetAuthorizationHeader(user.Id);

        // Seed test data for each endpoint
        if (routeTemplate.Contains("gym"))
        {
            await CreateGymViaEndpointAsync(user.Id, "Seed Gym");
        }
        else if (routeTemplate.Contains("Plan"))
        {
            await CreatePlanViaEndpointAsync(user.Id, "Seed Plan");
        }
        else if (routeTemplate.Contains("exercise"))
        {
            await CreateExerciseViaEndpointAsync(user.Id, "Seed Exercise");
        }

        var route = string.Format(routeTemplate, user.Id);

        var response = await Client.GetAsync(route);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var json = await ReadJsonAsync(response);
        AssertJsonArrayHasLegacyIdFields(json, $"endpoint {route} must return array items with _id");
    }

    #region Shared Assertion Helpers

    private static void AssertLegacyIdFieldPresent(JsonDocument json, string because)
    {
        Assert.That(json.RootElement.TryGetProperty("_id", out var id), Is.True, because);
        Assert.That(string.IsNullOrWhiteSpace(id.GetString()), Is.False);
        // Contract guard: API must use `_id` (legacy MongoDB naming), not `id`
        Assert.That(json.RootElement.TryGetProperty("id", out _), Is.False, $"response must not have 'id' field when '_id' is present (legacy compatibility)");
    }

    private static void AssertLegacyMsgFieldPresent(JsonDocument json, string because)
    {
        Assert.That(json.RootElement.TryGetProperty("msg", out var msg), Is.True, because);
        Assert.That(string.IsNullOrWhiteSpace(msg.GetString()), Is.False);
        // Contract guard: legacy clients expect `msg`; an accidental switch to `message` would break them.
        Assert.That(json.RootElement.TryGetProperty("message", out _), Is.False, $"response must not have 'message' field when 'msg' is present (legacy compatibility)");
    }

    private static void AssertLegacyReqFieldPresent(JsonDocument json, bool requireIdField, bool requireNameField)
    {
        Assert.That(json.RootElement.TryGetProperty("req", out var req), Is.True, "legacy clients expect 'req' field");
        if (requireIdField)
        {
            Assert.That(req.TryGetProperty("_id", out var userId), Is.True, "req must contain _id");
            Assert.That(string.IsNullOrWhiteSpace(userId.GetString()), Is.False);
        }
        if (requireNameField)
        {
            Assert.That(req.TryGetProperty("name", out var userName), Is.True, "req must contain name");
            Assert.That(string.IsNullOrWhiteSpace(userName.GetString()), Is.False);
        }
    }

    private static void AssertJsonArrayHasLegacyIdFields(JsonDocument json, string because)
    {
        Assert.That(json.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array), because);
        var array = json.RootElement.EnumerateArray().ToList();
        Assert.That(array, Is.Not.Empty, because);

        foreach (var item in array)
        {
            Assert.That(item.TryGetProperty("_id", out var id), Is.True, $"{because} - each item must have _id");
            Assert.That(string.IsNullOrWhiteSpace(id.GetString()), Is.False);
        }
    }

    private static void AssertEnumLookup(JsonElement lookup, string expectedId)
    {
        Assert.That(lookup.ValueKind, Is.EqualTo(JsonValueKind.Object));
        Assert.That(lookup.GetProperty("id").GetString(), Is.EqualTo(expectedId));
        Assert.That(string.IsNullOrWhiteSpace(lookup.GetProperty("name").GetString()), Is.False);
        Assert.That(string.IsNullOrWhiteSpace(lookup.GetProperty("displayName").GetString()), Is.False);
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
