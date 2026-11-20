using System.Net;
using System.Text.Json;

namespace LgymApi.IntegrationTests;

/// <summary>
/// Reusable test harness for validating HTTP response parity between old exception-based 
/// and new Result-pattern control flows.
/// </summary>
public sealed class HttpResponseParityTestHarness
{
    /// <summary>
    /// Asserts that a response represents a successful operation with correct status code.
    /// </summary>
    public void AssertSuccessResponse(
        HttpResponseMessage response,
        HttpStatusCode expectedStatusCode,
        string? because = null)
    {
        Assert.That(response.StatusCode, Is.EqualTo(expectedStatusCode), because ?? $"Expected status {expectedStatusCode} for success response");
    }

    /// <summary>
    /// Asserts that a response represents an error with ResponseMessageDto format (msg field).
    /// </summary>
    public async Task AssertErrorMessageResponseAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatusCode,
        string? expectedMessageContent = null,
        string? because = null)
    {
        Assert.That(response.StatusCode, Is.EqualTo(expectedStatusCode), because ?? $"Expected status {expectedStatusCode} for error response");

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        Assert.That(json.RootElement.TryGetProperty("msg", out var msgField), Is.True, "error response must contain 'msg' field in ResponseMessageDto format");

        var message = msgField.GetString();
        Assert.That(string.IsNullOrWhiteSpace(message), Is.False, "error message should not be empty");

        Assert.That(json.RootElement.TryGetProperty("message", out _), Is.False, "response must use 'msg' field, not 'message' (legacy compatibility)");

        if (!string.IsNullOrEmpty(expectedMessageContent))
        {
            Assert.That(message, Does.Contain(expectedMessageContent), $"error message should contain '{expectedMessageContent}'");
        }
    }

    /// <summary>
    /// Asserts that a response represents an error with custom payload.
    /// </summary>
    public async Task AssertErrorPayloadResponseAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatusCode,
        Action<JsonElement>? payloadAssertions = null,
        string? because = null)
    {
        Assert.That(response.StatusCode, Is.EqualTo(expectedStatusCode), because ?? $"Expected status {expectedStatusCode} for error with payload");

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        Assert.That(json.RootElement.TryGetProperty("msg", out _), Is.False, "custom payload responses should not be wrapped in ResponseMessageDto format");

        if (payloadAssertions != null)
        {
            payloadAssertions(json.RootElement);
        }
    }

    /// <summary>
    /// Extracts and deserializes the response body as the given type.
    /// </summary>
    public async Task<T?> GetResponseBodyAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrEmpty(content))
        {
            return default;
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Deserialize<T>(content, options);
    }

    /// <summary>
    /// Extracts the raw JSON document from the response for advanced assertions.
    /// </summary>
    public async Task<JsonDocument> GetResponseJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }

    /// <summary>
    /// Asserts that success and error responses maintain consistent JSON field naming conventions.
    /// </summary>
    public async Task AssertResponseParityForErrorMessagesAsync(
        HttpResponseMessage oldResponse,
        HttpResponseMessage newResponse,
        HttpStatusCode expectedStatusCode,
        string? because = null)
    {
        Assert.That(oldResponse.StatusCode, Is.EqualTo(expectedStatusCode), because ?? "old endpoint status code should match expectation");
        Assert.That(newResponse.StatusCode, Is.EqualTo(expectedStatusCode), because ?? "new endpoint status code should match old endpoint");

        var oldContent = await oldResponse.Content.ReadAsStringAsync();
        var newContent = await newResponse.Content.ReadAsStringAsync();

        var oldJson = JsonDocument.Parse(oldContent);
        var newJson = JsonDocument.Parse(newContent);

        Assert.That(oldJson.RootElement.TryGetProperty("msg", out var oldMsg), Is.True, "old response must have 'msg' field");
        Assert.That(newJson.RootElement.TryGetProperty("msg", out var newMsg), Is.True, "new response must have 'msg' field");

        Assert.That(string.IsNullOrWhiteSpace(oldMsg.GetString()), Is.False, "old message should have content");
        Assert.That(string.IsNullOrWhiteSpace(newMsg.GetString()), Is.False, "new message should have content");

        Assert.That(oldJson.RootElement.TryGetProperty("message", out _), Is.False, "old response must use 'msg', not 'message'");
        Assert.That(newJson.RootElement.TryGetProperty("message", out _), Is.False, "new response must use 'msg', not 'message'");
    }

    /// <summary>
    /// Asserts that success responses maintain required fields.
    /// </summary>
    public async Task AssertSuccessResponseHasRequiredFieldsAsync(
        HttpResponseMessage successResponse,
        HttpStatusCode expectedStatusCode,
        IEnumerable<string> requiredFieldNames,
        string? because = null)
    {
        Assert.That(successResponse.StatusCode, Is.EqualTo(expectedStatusCode), because ?? $"Expected status {expectedStatusCode}");

        var content = await successResponse.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        foreach (var fieldName in requiredFieldNames)
        {
            Assert.That(json.RootElement.TryGetProperty(fieldName, out _), Is.True, $"response must have '{fieldName}' field");
        }
    }

    /// <summary>
    /// Compares error message responses to ensure parity.
    /// </summary>
    public void AssertResponseContentParity(
        string oldResponseContent,
        string newResponseContent,
        string? because = null)
    {
        var oldJson = JsonDocument.Parse(oldResponseContent);
        var newJson = JsonDocument.Parse(newResponseContent);

        var oldNormalized = JsonSerializer.Serialize(oldJson.RootElement,
            new JsonSerializerOptions { WriteIndented = false });
        var newNormalized = JsonSerializer.Serialize(newJson.RootElement,
            new JsonSerializerOptions { WriteIndented = false });

        Assert.That(oldNormalized, Is.EqualTo(newNormalized), because ?? "error response content should be identical between old and new implementations");
    }
}
