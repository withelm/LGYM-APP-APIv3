using System.Net;
using System.Text;
using System.Text.Json;
using LgymApi.ExternalE2ETests.Configuration;

namespace LgymApi.ExternalE2ETests.Lifecycle;

internal interface IExternalScenarioClock
{
    DateTimeOffset UtcNow { get; }

    ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

internal sealed class SystemExternalScenarioClock : IExternalScenarioClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        new(Task.Delay(delay, cancellationToken));
}

internal sealed class ExternalApiRecoveryException : InvalidOperationException
{
    public ExternalApiRecoveryException(string gate)
        : base($"External API recovery gate failed: {gate}.")
    {
    }
}

internal interface IExternalScenarioApiRecoveryGateway
{
    ValueTask WaitForRecoveryAsync(ExternalE2EOptions options, CancellationToken cancellationToken);
}

internal sealed class ExternalApiRecoveryGate : IExternalScenarioApiRecoveryGateway, IDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(250);
    private readonly IExternalScenarioClock _clock;
    private readonly HttpClient _client;

    public ExternalApiRecoveryGate(HttpMessageHandler handler, IExternalScenarioClock clock)
    {
        _client = new HttpClient(handler ?? throw new ArgumentNullException(nameof(handler)), disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public ValueTask WaitForRecoveryAsync(ExternalE2EOptions options, CancellationToken cancellationToken) =>
        WaitForRecoveryAsync(options.ApiBaseUrl, options.ApiRecoveryTimeout, cancellationToken);

    public async ValueTask WaitForRecoveryAsync(Uri apiBaseUri, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (apiBaseUri is null || timeout <= TimeSpan.Zero)
        {
            throw new ExternalApiRecoveryException("configuration");
        }

        var deadline = _clock.UtcNow + timeout;
        using var recoveryCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        recoveryCancellation.CancelAfter(timeout);

        while (true)
        {
            try
            {
                await RequireHealthyResponseAsync(apiBaseUri, recoveryCancellation.Token);
                await RequireUnauthorizedLoginAsync(apiBaseUri, recoveryCancellation.Token);
                return;
            }
            catch (HttpRequestException) when (_clock.UtcNow < deadline)
            {
                var remaining = deadline - _clock.UtcNow;
                await _clock.DelayAsync(remaining < RetryDelay ? remaining : RetryDelay, recoveryCancellation.Token);
            }
            catch (HttpRequestException)
            {
                throw new ExternalApiRecoveryException("timeout");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw new ExternalApiRecoveryException("timeout");
            }
        }
    }

    public void Dispose() => _client.Dispose();

    private async Task RequireHealthyResponseAsync(Uri apiBaseUri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(apiBaseUri, "/health/live"));
        using var response = await _client.SendAsync(request, cancellationToken);

        if (IsRedirect(response.StatusCode) || response.StatusCode != HttpStatusCode.OK)
        {
            throw new ExternalApiRecoveryException("health");
        }

        string body;
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("status", out var status) ||
                status.ValueKind != JsonValueKind.String ||
                !string.Equals(status.GetString(), "ok", StringComparison.Ordinal))
            {
                throw new ExternalApiRecoveryException("health");
            }
        }
        catch (JsonException)
        {
            throw new ExternalApiRecoveryException("health");
        }
    }

    private async Task RequireUnauthorizedLoginAsync(Uri apiBaseUri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(apiBaseUri, "/api/login"))
        {
            Content = new StringContent(
                "{\"name\":\"external-e2e-invalid-user\",\"password\":\"external-e2e-invalid-password\"}",
                Encoding.UTF8,
                "application/json")
        };
        using var response = await _client.SendAsync(request, cancellationToken);

        if (IsRedirect(response.StatusCode) || response.StatusCode != HttpStatusCode.Unauthorized)
        {
            throw new ExternalApiRecoveryException("invalid-login");
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode) => (int)statusCode is >= 300 and < 400;
}
