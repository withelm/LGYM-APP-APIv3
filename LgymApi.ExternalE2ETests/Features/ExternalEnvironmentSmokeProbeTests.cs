using System.Net;
using System.Net.Sockets;
using System.Text;
using LgymApi.ExternalE2ETests.Browser;
using NUnit.Framework;

namespace LgymApi.ExternalE2ETests.Features;

[TestFixture]
[Category("ExternalSmokeContract")]
public sealed class ExternalEnvironmentSmokeProbeTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    [Test]
    public async Task Test_probe_when_visible_main_and_configured_api_fetch_are_observed_succeeds()
    {
        await using var api = await LocalHttpFixture.StartAsync(_ => "ok");
        await using var web = await LocalHttpFixture.StartAsync(_ => HtmlWithMainAndFetch(api.Uri));

        await UseProbeAsync(web.Uri, api.Uri, async probe =>
        {
            await probe.NavigateAsync();
            await probe.RequireUnauthenticatedSurfaceAsync();
            await probe.RequireConfiguredApiRequestAsync();
        });
    }

    [Test]
    public async Task Test_probe_when_application_fetch_targets_foreign_origin_rejects_it()
    {
        await using var expectedApi = await LocalHttpFixture.StartAsync(_ => "ok");
        await using var foreignApi = await LocalHttpFixture.StartAsync(_ => "ok");
        await using var web = await LocalHttpFixture.StartAsync(_ => HtmlWithMainAndFetch(foreignApi.Uri));

        await UseProbeAsync(web.Uri, expectedApi.Uri, async probe =>
        {
            await probe.NavigateAsync();
            await probe.RequireUnauthenticatedSurfaceAsync();

            var exception = Assert.ThrowsAsync<InvalidOperationException>(probe.RequireConfiguredApiRequestAsync);
            Assert.That(exception!.Message, Does.Contain("configured API origin"));
        });
    }

    [Test]
    public async Task Test_probe_when_visible_main_is_missing_rejects_the_surface()
    {
        await using var api = await LocalHttpFixture.StartAsync(_ => "ok");
        await using var web = await LocalHttpFixture.StartAsync(_ => HtmlWithoutMain(api.Uri));

        await UseProbeAsync(web.Uri, api.Uri, async probe =>
        {
            await probe.NavigateAsync();
            Assert.ThrowsAsync<TimeoutException>(probe.RequireUnauthenticatedSurfaceAsync);
        });
    }

    [Test]
    public async Task Test_probe_when_no_application_request_targets_configured_origin_rejects_it()
    {
        await using var api = await LocalHttpFixture.StartAsync(_ => "ok");
        await using var web = await LocalHttpFixture.StartAsync(_ => "<!doctype html><main>Unauthenticated application</main>");

        await UseProbeAsync(web.Uri, api.Uri, async probe =>
        {
            await probe.NavigateAsync();
            await probe.RequireUnauthenticatedSurfaceAsync();
            Assert.ThrowsAsync<TimeoutException>(probe.RequireConfiguredApiRequestAsync);
        });
    }

    [Test]
    public async Task Test_probe_when_navigation_redirects_rejects_it()
    {
        await using var api = await LocalHttpFixture.StartAsync(_ => "ok");
        await using var destination = await LocalHttpFixture.StartAsync(_ => "<!doctype html><main>Unauthenticated application</main>");
        await using var web = await LocalHttpFixture.StartRedirectAsync(destination.Uri);

        await UseProbeAsync(web.Uri, api.Uri, probe =>
        {
            var exception = Assert.ThrowsAsync<InvalidOperationException>(probe.NavigateAsync);
            Assert.That(exception!.Message, Does.Contain("redirected"));
            return Task.CompletedTask;
        });
    }

    private static async Task UseProbeAsync(Uri webUri, Uri apiUri, Func<ExternalEnvironmentSmokeProbe, Task> test)
    {
        await using var runLease = await ExternalBrowserRunLease.CreateAsync(
            new ExternalBrowserRunLeaseOptions(Timeout, "chrome"),
            CancellationToken.None);
        await using var scenarioLease = await runLease.AcquireScenarioAsync(
            new ExternalBrowserScenarioLeaseOptions(
                webUri,
                Timeout,
                Timeout,
                Path.Combine(Path.GetTempPath(), "lgym-external-e2e-smoke", Guid.NewGuid().ToString("N")),
                []),
            CancellationToken.None);

        await test(new ExternalEnvironmentSmokeProbe(scenarioLease.Page, webUri, apiUri, Timeout));
    }

    private static string HtmlWithMainAndFetch(Uri apiUri) =>
        $"<!doctype html><main>Unauthenticated application</main><script>fetch('{apiUri}api/smoke')</script>";

    private static string HtmlWithoutMain(Uri apiUri) =>
        $"<!doctype html><div>Unauthenticated application</div><script>fetch('{apiUri}api/smoke')</script>";

    private sealed class LocalHttpFixture : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cancellationSource = new();
        private readonly TcpListener _listener;
        private readonly string? _redirectLocation;
        private readonly Task _serveTask;
        private readonly Func<string, string> _content;

        private LocalHttpFixture(TcpListener listener, Func<string, string> content, string? redirectLocation)
        {
            _listener = listener;
            _content = content;
            _redirectLocation = redirectLocation;
            Uri = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");
            _serveTask = ServeAsync();
        }

        public Uri Uri { get; }

        public static Task<LocalHttpFixture> StartAsync(Func<string, string> content) => StartAsync(content, null);

        public static Task<LocalHttpFixture> StartRedirectAsync(Uri destination) =>
            StartAsync(_ => string.Empty, destination.AbsoluteUri);

        public async ValueTask DisposeAsync()
        {
            await _cancellationSource.CancelAsync();
            _listener.Stop();

            try
            {
                await _serveTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException) when (_cancellationSource.IsCancellationRequested)
            {
            }

            _cancellationSource.Dispose();
        }

        private static Task<LocalHttpFixture> StartAsync(Func<string, string> content, string? redirectLocation)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return Task.FromResult(new LocalHttpFixture(listener, content, redirectLocation));
        }

        private async Task ServeAsync()
        {
            try
            {
                while (true)
                {
                    var client = await _listener.AcceptTcpClientAsync(_cancellationSource.Token);
                    _ = ServeClientAsync(client, _cancellationSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task ServeClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using var ownedClient = client;
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            var requestLine = await reader.ReadLineAsync(cancellationToken);
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellationToken)))
            {
            }

            var isApiRequest = requestLine?.Contains("/api/", StringComparison.Ordinal) is true;
            var body = _content(requestLine ?? string.Empty);
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var response = _redirectLocation is not null && !isApiRequest
                ? $"HTTP/1.1 302 Found\r\nLocation: {_redirectLocation}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                : $"HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";

            await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
            if (_redirectLocation is null || isApiRequest)
            {
                await stream.WriteAsync(bodyBytes, cancellationToken);
            }
        }
    }
}
