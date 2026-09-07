using System.Net;
using LgymApi.ExternalE2ETests.Configuration;
using NUnit.Framework;

namespace LgymApi.ExternalE2ETests.Lifecycle;

[TestFixture]
[Category("ExternalScenarioLifecycle")]
public sealed class ExternalScenarioResetLifecycleTests
{
    [Test]
    public async Task Test_prepare_when_all_gates_succeed_restores_recovers_then_acquires_and_disposes()
    {
        var events = new List<string>();
        var lifecycle = new ExternalScenarioResetLifecycle(
            new FakeOptionsSource(events),
            new FakeRestoreGateway(events),
            new FakeApiRecoveryGateway(events),
            new FakeBrowserGateway(events));

        var lease = await lifecycle.PrepareAsync("EXT-LIFECYCLE-001", CancellationToken.None);
        await lifecycle.CompleteAsync(lease, failed: false);

        Assert.That(events, Is.EqualTo(["load", "restore", "recover", "acquire", "dispose"]));
    }

    [Test]
    public async Task Test_prepare_when_restore_fails_preserves_the_primary_error_and_never_acquires_browser()
    {
        var events = new List<string>();
        var primaryError = new InvalidOperationException("restore failed");
        var lifecycle = new ExternalScenarioResetLifecycle(
            new FakeOptionsSource(events),
            new FakeRestoreGateway(events, primaryError),
            new FakeApiRecoveryGateway(events),
            new FakeBrowserGateway(events));

        var thrown = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await lifecycle.PrepareAsync("EXT-LIFECYCLE-001", CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(thrown, Is.SameAs(primaryError));
            Assert.That(events, Is.EqualTo(["load", "restore"]));
        });
    }

    [Test]
    public async Task Test_prepare_when_api_recovery_fails_never_acquires_browser()
    {
        var events = new List<string>();
        var lifecycle = new ExternalScenarioResetLifecycle(
            new FakeOptionsSource(events),
            new FakeRestoreGateway(events),
            new FakeApiRecoveryGateway(events, new ExternalApiRecoveryException("health")),
            new FakeBrowserGateway(events));

        Assert.ThrowsAsync<ExternalApiRecoveryException>(
            async () => await lifecycle.PrepareAsync("EXT-LIFECYCLE-001", CancellationToken.None));

        Assert.That(events, Is.EqualTo(["load", "restore", "recover"]));
    }

    [Test]
    public async Task Test_complete_when_scenario_failed_marks_lease_before_disposal()
    {
        var events = new List<string>();
        var lifecycle = new ExternalScenarioResetLifecycle(
            new FakeOptionsSource(events),
            new FakeRestoreGateway(events),
            new FakeApiRecoveryGateway(events),
            new FakeBrowserGateway(events));

        var lease = await lifecycle.PrepareAsync("EXT-LIFECYCLE-001", CancellationToken.None);
        await lifecycle.CompleteAsync(lease, failed: true);

        Assert.That(events, Is.EqualTo(["load", "restore", "recover", "acquire", "mark-failed", "dispose"]));
    }

    [Test]
    public async Task Test_prepare_when_first_scenario_is_active_serializes_the_next_scenario()
    {
        var events = new List<string>();
        var lifecycle = new ExternalScenarioResetLifecycle(
            new FakeOptionsSource(events),
            new FakeRestoreGateway(events),
            new FakeApiRecoveryGateway(events),
            new FakeBrowserGateway(events));

        var firstLease = await lifecycle.PrepareAsync("EXT-LIFECYCLE-001", CancellationToken.None);
        var secondPreparation = lifecycle.PrepareAsync("EXT-LIFECYCLE-002", CancellationToken.None).AsTask();

        Assert.That(secondPreparation.IsCompleted, Is.False);
        await lifecycle.CompleteAsync(firstLease, failed: false);
        var secondLease = await secondPreparation;
        await lifecycle.CompleteAsync(secondLease, failed: false);

        Assert.That(events, Is.EqualTo(
            ["load", "restore", "recover", "acquire", "dispose", "load", "restore", "recover", "acquire", "dispose"]));
    }

    [Test]
    public async Task Test_recovery_when_health_and_invalid_login_are_valid_uses_the_public_contract_in_order()
    {
        using var handler = new SequenceHttpHandler(
            JsonResponse(HttpStatusCode.OK, "{\"status\":\"ok\"}"),
            JsonResponse(HttpStatusCode.Unauthorized, string.Empty));
        var recovery = new ExternalApiRecoveryGate(handler, new TestClock());

        await recovery.WaitForRecoveryAsync(new Uri("http://127.0.0.1:5080"), TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.That(handler.Requests, Is.EqualTo(["GET /health/live", "POST /api/login"]));
    }

    [TestCase(HttpStatusCode.Found, "{\"status\":\"ok\"}")]
    [TestCase(HttpStatusCode.InternalServerError, "{\"status\":\"ok\"}")]
    [TestCase(HttpStatusCode.OK, "{\"status\":true}")]
    public void Test_recovery_when_health_response_is_invalid_fails_before_invalid_login(
        HttpStatusCode statusCode,
        string body)
    {
        using var handler = new SequenceHttpHandler(JsonResponse(statusCode, body));
        var recovery = new ExternalApiRecoveryGate(handler, new TestClock());

        Assert.ThrowsAsync<ExternalApiRecoveryException>(
            async () => await recovery.WaitForRecoveryAsync(new Uri("http://127.0.0.1:5080"), TimeSpan.FromSeconds(1), CancellationToken.None));

        Assert.That(handler.Requests, Is.EqualTo(["GET /health/live"]));
    }

    [TestCase(HttpStatusCode.OK)]
    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.Found)]
    public void Test_recovery_when_invalid_login_is_not_exactly_401_fails_before_browser_acquisition(HttpStatusCode statusCode)
    {
        using var handler = new SequenceHttpHandler(
            JsonResponse(HttpStatusCode.OK, "{\"status\":\"ok\"}"),
            JsonResponse(statusCode, string.Empty));
        var recovery = new ExternalApiRecoveryGate(handler, new TestClock());

        Assert.ThrowsAsync<ExternalApiRecoveryException>(
            async () => await recovery.WaitForRecoveryAsync(new Uri("http://127.0.0.1:5080"), TimeSpan.FromSeconds(1), CancellationToken.None));

        Assert.That(handler.Requests, Is.EqualTo(["GET /health/live", "POST /api/login"]));
    }

    [Test]
    public void Test_recovery_when_health_never_connects_times_out_without_invalid_login()
    {
        using var handler = new SequenceHttpHandler(
            new HttpRequestException("connection refused"),
            new HttpRequestException("connection refused"),
            new HttpRequestException("connection refused"),
            new HttpRequestException("connection refused"),
            new HttpRequestException("connection refused"));
        var recovery = new ExternalApiRecoveryGate(handler, new TestClock());

        Assert.ThrowsAsync<ExternalApiRecoveryException>(
            async () => await recovery.WaitForRecoveryAsync(new Uri("http://127.0.0.1:5080"), TimeSpan.FromSeconds(1), CancellationToken.None));

        Assert.That(handler.Requests, Has.Count.EqualTo(5));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body) => new(statusCode)
    {
        Content = new StringContent(body)
    };

    private sealed class FakeOptionsSource(List<string> events) : IExternalScenarioOptionsSource
    {
        public ExternalE2EOptions Load()
        {
            events.Add("load");
            return new ExternalE2EOptions
            {
                ApiBaseUrl = new Uri("http://127.0.0.1:5080"),
                WebBaseUrl = new Uri("http://127.0.0.1:8081"),
                ConnectionString = "Host=localhost;Database=lgym_external_e2e;Username=lgym_external_e2e_runner;Password=canary",
                ConnectionIdentity = new ExternalE2EConnectionIdentity("localhost", "lgym_external_e2e", "lgym_external_e2e_runner"),
                BaselineDumpPath = "C:\\temp\\baseline.bak",
                ExpectedDatabaseName = "lgym_external_e2e",
                ExpectedDatabaseMarker = "lgym_external_e2e_v1",
                RestoreTimeout = TimeSpan.FromSeconds(30),
                ApiRecoveryTimeout = TimeSpan.FromSeconds(30),
                BrowserTimeout = TimeSpan.FromSeconds(30),
                DiagnosticTimeout = TimeSpan.FromSeconds(30),
                ArtifactRoot = "C:\\temp\\artifacts"
            };
        }
    }

    private sealed class FakeRestoreGateway(List<string> events, Exception? exception = null) : IExternalScenarioRestoreGateway
    {
        public ValueTask RestoreAsync(ExternalE2EOptions options, CancellationToken cancellationToken)
        {
            events.Add("restore");
            return exception is null
                ? ValueTask.CompletedTask
                : ValueTask.FromException(exception);
        }
    }

    private sealed class FakeApiRecoveryGateway(List<string> events, Exception? exception = null) : IExternalScenarioApiRecoveryGateway
    {
        public ValueTask WaitForRecoveryAsync(ExternalE2EOptions options, CancellationToken cancellationToken)
        {
            events.Add("recover");
            return exception is null
                ? ValueTask.CompletedTask
                : ValueTask.FromException(exception);
        }
    }

    private sealed class FakeBrowserGateway(List<string> events) : IExternalScenarioBrowserGateway
    {
        public ValueTask<IExternalScenarioBrowserLease> AcquireAsync(
            ExternalE2EOptions options,
            string scenarioId,
            CancellationToken cancellationToken)
        {
            events.Add("acquire");
            return ValueTask.FromResult<IExternalScenarioBrowserLease>(new FakeBrowserLease(events));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeBrowserLease(List<string> events) : IExternalScenarioBrowserLease
    {
        public void MarkFailed() => events.Add("mark-failed");

        public ValueTask DisposeAsync()
        {
            events.Add("dispose");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestClock : IExternalScenarioClock
    {
        public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.UnixEpoch;

        public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            UtcNow += delay;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SequenceHttpHandler(params object[] responses) : HttpMessageHandler
    {
        private readonly Queue<object> _responses = new(responses);

        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            return _responses.Dequeue() switch
            {
                HttpResponseMessage response => Task.FromResult(response),
                Exception exception => Task.FromException<HttpResponseMessage>(exception),
                _ => throw new InvalidOperationException("Unsupported HTTP response fixture.")
            };
        }
    }
}
