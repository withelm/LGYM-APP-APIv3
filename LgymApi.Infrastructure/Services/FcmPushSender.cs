using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using LgymApi.BackgroundWorker.Common.Push;
using LgymApi.BackgroundWorker.Common.Push.Models;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Options;
using Microsoft.Extensions.Logging;

namespace LgymApi.Infrastructure.Services;

public sealed class FcmPushSender : IPushProviderSender
{
    private const string MessagingScope = "https://www.googleapis.com/auth/firebase.messaging";
    private const string NotificationTitle = "LGYM";
    private const string NotificationBody = "You have a new notification.";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PushNotificationOptions _options;
    private readonly ILogger<FcmPushSender> _logger;

    public FcmPushSender(
        IHttpClientFactory httpClientFactory,
        PushNotificationOptions options,
        ILogger<FcmPushSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<PushSendAttemptResult> SendAsync(
        PushInstallation installation,
        PushEventPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsSendEnabled)
        {
            return new PushSendAttemptResult(
                PushSendOutcome.Skipped,
                "Skipped",
                null,
                "push-disabled",
                "Push notifications are disabled.");
        }

        var accessToken = await GetAccessTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildSendUrl())
        {
            Content = BuildRequestContent(installation, payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var client = _httpClientFactory.CreateClient(nameof(FcmPushSender));
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var providerMessageId = TryExtractValue(body, "name");
            return new PushSendAttemptResult(
                PushSendOutcome.Sent,
                response.StatusCode.ToString(),
                providerMessageId,
                null,
                Summarize(body));
        }

        var summary = Summarize(body);
        var errorCode = TryExtractValue(body, "status") ?? response.StatusCode.ToString();
        var outcome = ClassifyFailure(response.StatusCode, summary);
        _logger.LogWarning(
            "FCM send failed for installation {InstallationId}, event {EventId}, category {Category} with provider status {ProviderStatus} and outcome {Outcome}.",
            installation.InstallationId,
            payload.EventId,
            payload.Type,
            response.StatusCode,
            outcome);

        return new PushSendAttemptResult(
            outcome,
            response.StatusCode.ToString(),
            null,
            errorCode,
            summary);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        GoogleCredential credential;
        if (!string.IsNullOrWhiteSpace(_options.Fcm.CredentialsJson))
        {
            credential = CreateCredentialFromJson(_options.Fcm.CredentialsJson);
        }
        else if (!string.IsNullOrWhiteSpace(_options.Fcm.CredentialsPath))
        {
            credential = CreateCredentialFromFile(_options.Fcm.CredentialsPath);
        }
        else
        {
            throw new InvalidOperationException("FCM credentials are not configured.");
        }

        credential = credential.CreateScoped(MessagingScope);
        return await credential.UnderlyingCredential.GetAccessTokenForRequestAsync(null, cancellationToken);
    }

    private static GoogleCredential CreateCredentialFromJson(string credentialsJson)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(credentialsJson));
        return CredentialFactory.FromStream<ServiceAccountCredential>(stream).ToGoogleCredential();
    }

    private static GoogleCredential CreateCredentialFromFile(string credentialsPath)
    {
        using var stream = File.OpenRead(credentialsPath);
        return CredentialFactory.FromStream<ServiceAccountCredential>(stream).ToGoogleCredential();
    }

    private StringContent BuildRequestContent(PushInstallation installation, PushEventPayload payload)
    {
        var request = new
        {
            message = new
            {
                token = installation.FcmToken,
                notification = new
                {
                    title = NotificationTitle,
                    body = NotificationBody
                },
                android = new
                {
                    priority = "HIGH"
                },
                data = new Dictionary<string, string>
                {
                    ["schemaVersion"] = payload.SchemaVersion.ToString(),
                    ["type"] = payload.Type,
                    ["eventId"] = payload.EventId,
                    ["entityId"] = payload.EntityId ?? string.Empty,
                    ["inAppNotificationId"] = payload.InAppNotificationId ?? string.Empty,
                    ["deeplink"] = payload.Deeplink ?? string.Empty
                }
            }
        };

        var json = JsonSerializer.Serialize(request);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private string BuildSendUrl()
    {
        return $"{_options.Fcm.BaseUrl}/v1/projects/{_options.Fcm.ProjectId}/messages:send";
    }

    private static PushSendOutcome ClassifyFailure(HttpStatusCode statusCode, string summary)
    {
        if (ContainsAny(summary, "unregistered", "registration-token-not-registered", "invalid registration token", "not a valid fcm registration token"))
        {
            return PushSendOutcome.InvalidToken;
        }

        if (statusCode == HttpStatusCode.TooManyRequests
            || statusCode == HttpStatusCode.RequestTimeout
            || statusCode == HttpStatusCode.BadGateway
            || statusCode == HttpStatusCode.ServiceUnavailable
            || statusCode == HttpStatusCode.GatewayTimeout
            || (int)statusCode >= 500)
        {
            return PushSendOutcome.TransientFailure;
        }

        if (ContainsAny(summary, "unavailable", "internal", "timeout", "temporar"))
        {
            return PushSendOutcome.TransientFailure;
        }

        return PushSendOutcome.PermanentFailure;
    }

    private static string? TryExtractValue(string body, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (document.RootElement.TryGetProperty(propertyName, out var directValue) && directValue.ValueKind == JsonValueKind.String)
                {
                    return directValue.GetString();
                }

                if (document.RootElement.TryGetProperty("error", out var error)
                    && error.ValueKind == JsonValueKind.Object
                    && error.TryGetProperty(propertyName, out var nestedValue)
                    && nestedValue.ValueKind == JsonValueKind.String)
                {
                    return nestedValue.GetString();
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string Summarize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        return normalized.Length <= 1000 ? normalized : normalized[..1000];
    }
}
