using System.Net;
using System.Net.Http.Json;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.IntegrationTests;

/// <summary>
/// Integration tests demonstrating HTTP response parity between old exception-based
/// and new Result-pattern control flows.
/// 
/// These tests validate that the HttpResponseParityTestHarness works correctly
/// by testing both success and error response scenarios with proper status codes
/// and message formatting (using "msg" field, not "message").
/// </summary>
[TestFixture]
public sealed class HttpResponseParityTests : IntegrationTestBase
{
    private HttpResponseParityTestHarness _harness = null!;

    [SetUp]
    public void SetUpHarness()
    {
        _harness = new HttpResponseParityTestHarness();
    }

    /// <summary>
    /// Validates that error responses use "msg" field format (not "message").
    /// This is critical for Result pattern parity - both old and new implementations
    /// must use the same JSON field name for backward compatibility.
    /// </summary>
    [Test]
    public async Task ErrorResponse_UsesMsgFieldFormat_NotMessageField()
    {
        var user = await SeedUserAsync(
            name: "parity_test_user",
            email: "parity_test@example.com");
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync($"/api/exercise/{user.Id}/getAllUserExercises");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        var json = await _harness.GetResponseJsonAsync(response);
        Assert.That(json.RootElement.TryGetProperty("msg", out _), Is.True, "error responses must use 'msg' field for backward compatibility");

        Assert.That(json.RootElement.TryGetProperty("message", out _), Is.False, "error responses must not use 'message' field (wrong capitalization)");
    }

    /// <summary>
    /// Validates that success responses have proper JSON array structure.
    /// </summary>
    [Test]
    public async Task SuccessResponse_HasExpectedJsonFields()
    {
        var user = await SeedUserAsync(
            name: "success_test_user",
            email: "success_test@example.com");
        SetAuthorizationHeader(user.Id);

        var response = await Client.GetAsync($"/api/gym/{user.Id}/getGyms");

        _harness.AssertSuccessResponse(response, HttpStatusCode.OK);

        var json = await _harness.GetResponseJsonAsync(response);
        Assert.That(json.RootElement.ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Array), "getGyms should return an array");
    }

    /// <summary>
    /// Validates that create endpoints return message format with "msg" field.
    /// This demonstrates the harness can validate both message and success response formats.
    /// </summary>
    [Test]
    public async Task CreateEndpoint_ReturnsMessageResponse()
    {
        var user = await SeedUserAsync(
            name: "create_test_user",
            email: "create_test@example.com");
        SetAuthorizationHeader(user.Id);

        var response = await Client.PostAsJsonAsync($"/api/gym/{user.Id}/addGym",
            new { name = "Test Gym for Create" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await _harness.AssertErrorMessageResponseAsync(response, HttpStatusCode.OK,
            because: "create endpoint should return ResponseMessageDto with success message");
    }

    /// <summary>
    /// Validates that forbidden operations return 403 with "msg" field format.
    /// Tests error response validation across different HTTP status codes.
    /// </summary>
    [Test]
    public async Task ForbiddenOperation_Returns403WithMsgField()
    {
        var user1 = await SeedUserAsync(
            name: "user1_forbidden",
            email: "user1_forbidden@example.com");
        var user2 = await SeedUserAsync(
            name: "user2_forbidden",
            email: "user2_forbidden@example.com");

        SetAuthorizationHeader(user1.Id);
        var response = await Client.PostAsJsonAsync($"/api/appConfig/createNewAppVersion/{user2.Id}", new
        {
            platform = "Android",
            minRequiredVersion = "1.0.0",
            latestVersion = "2.0.0"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        await _harness.AssertErrorMessageResponseAsync(response, HttpStatusCode.Forbidden,
            because: "forbidden operation should return ResponseMessageDto error format");
    }

}
