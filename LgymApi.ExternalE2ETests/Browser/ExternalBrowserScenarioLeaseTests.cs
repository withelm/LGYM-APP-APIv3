using System.Text;
using System.Text.Json;
using Microsoft.Playwright;
using NUnit.Framework;

namespace LgymApi.ExternalE2ETests.Browser;

[TestFixture]
[Category("BrowserLease")]
public sealed class ExternalBrowserScenarioLeaseTests
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(10);

    [Test]
    public async Task Test_scenario_lease_isolates_browser_state_when_previous_scenario_mutates_it()
    {
        await using var fixture = await LocalStaticFixture.StartAsync();
        await using var runLease = await ExternalBrowserRunLease.CreateAsync(
            CreateRunOptions(),
            CancellationToken.None);

        var diagnosticsPath = NewDiagnosticsPath();
        await using (var firstLease = await runLease.AcquireScenarioAsync(
                         CreateScenarioOptions(fixture.HomeUri, diagnosticsPath),
                         CancellationToken.None))
        {
            await firstLease.Page.GotoAsync(fixture.HomeUri.AbsoluteUri);
            await firstLease.Page.EvaluateAsync("""
                async () => {
                    document.cookie = 'scenario-cookie=present';
                    localStorage.setItem('scenario-local', 'present');
                    sessionStorage.setItem('scenario-session', 'present');
                    const databaseRequest = indexedDB.open('scenario-database', 1);
                    databaseRequest.onupgradeneeded = () => databaseRequest.result.createObjectStore('records');
                    await new Promise((resolve, reject) => {
                        databaseRequest.onsuccess = () => resolve();
                        databaseRequest.onerror = () => reject(databaseRequest.error);
                    });
                    const cache = await caches.open('scenario-cache');
                    await cache.put('/cached-state', new Response('present'));
                    await navigator.serviceWorker.register('/worker.js');
                    await navigator.serviceWorker.ready;
                }
                """);
        }

        await using var secondLease = await runLease.AcquireScenarioAsync(
            CreateScenarioOptions(fixture.HomeUri, diagnosticsPath),
            CancellationToken.None);
        await secondLease.Page.GotoAsync(fixture.HomeUri.AbsoluteUri);

        var observedState = await ReadStateAsync(secondLease.Page);

        Assert.Multiple(() =>
        {
            Assert.That(observedState.Cookie, Is.Empty);
            Assert.That(observedState.LocalStorage, Has.Count.Zero);
            Assert.That(observedState.SessionStorage, Has.Count.Zero);
            Assert.That(observedState.IndexedDatabases, Has.Count.Zero);
            Assert.That(observedState.Caches, Has.Count.Zero);
            Assert.That(observedState.ServiceWorkers, Has.Count.Zero);
        });
    }

    [Test]
    public async Task Test_scenario_lease_removes_diagnostics_when_scenario_succeeds()
    {
        await using var fixture = await LocalStaticFixture.StartAsync();
        await using var runLease = await ExternalBrowserRunLease.CreateAsync(
            CreateRunOptions(),
            CancellationToken.None);

        var diagnosticsPath = NewDiagnosticsPath();
        await using (var scenarioLease = await runLease.AcquireScenarioAsync(
                         CreateScenarioOptions(fixture.HomeUri, diagnosticsPath),
                         CancellationToken.None))
        {
            await scenarioLease.Page.GotoAsync(fixture.HomeUri.AbsoluteUri);
        }

        Assert.That(Directory.Exists(diagnosticsPath), Is.False);
    }

    [Test]
    public async Task Test_scenario_lease_retains_only_bounded_sanitized_diagnostics_when_scenario_fails()
    {
        const string secretCanary = "secret-canary-not-for-diagnostics";
        const string tokenCanary = "token-canary-not-for-diagnostics";
        var diagnosticsPath = NewDiagnosticsPath();
        await using var fixture = await LocalStaticFixture.StartAsync();
        await using var runLease = await ExternalBrowserRunLease.CreateAsync(
            CreateRunOptions(),
            CancellationToken.None);

        var scenarioLease = await runLease.AcquireScenarioAsync(
            CreateScenarioOptions(
                fixture.HomeUri,
                diagnosticsPath,
                [secretCanary, tokenCanary, diagnosticsPath]),
            CancellationToken.None);
        await using (scenarioLease)
        {
            await scenarioLease.Page.GotoAsync(fixture.HomeUri.AbsoluteUri);
            await scenarioLease.Page.EvaluateAsync($"localStorage.setItem('secret', '{secretCanary}')");
            scenarioLease.MarkFailed();
        }

        var receipt = scenarioLease.CleanupReceipt;
        var artifacts = Directory.EnumerateFiles(diagnosticsPath).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(receipt.Failed, Is.True);
            Assert.That(receipt.ContextClosed, Is.True);
            Assert.That(receipt.TraceStopped, Is.True);
            Assert.That(artifacts, Is.Not.Empty);
            Assert.That(receipt.ArtifactFileNames, Is.EquivalentTo(artifacts.Select(Path.GetFileName)));
            Assert.That(
                artifacts.All(path => new FileInfo(path).Length <= ExternalBrowserScenarioLease.MaxDiagnosticArtifactBytes),
                Is.True);
            Assert.That(
                artifacts.All(path => DoesNotContainCanary(path, secretCanary, tokenCanary, diagnosticsPath)),
                Is.True);
        });

        TestContext.Progress.WriteLine(
            $"browser-lease-cleanup: failed={receipt.Failed}; contextClosed={receipt.ContextClosed}; " +
            $"traceStopped={receipt.TraceStopped}; artifacts={string.Join(',', receipt.ArtifactFileNames)}; " +
            $"cleanupFailures={string.Join(',', receipt.CleanupFailureStages)}");
    }

    [Test]
    public async Task Test_scenario_lease_closes_context_when_page_setup_fails()
    {
        await using var fixture = await LocalStaticFixture.StartAsync();
        await using var runLease = await ExternalBrowserRunLease.CreateAsync(
            CreateRunOptions(),
            CancellationToken.None);

        IBrowserContext? createdContext = null;

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await runLease.AcquireScenarioAsync(
                CreateScenarioOptions(fixture.HomeUri, NewDiagnosticsPath()),
                CancellationToken.None,
                context =>
                {
                    createdContext = context;
                    throw new InvalidOperationException("Injected page setup failure.");
                }));

        Assert.That(exception!.Message, Is.EqualTo("Injected page setup failure."));
        Assert.That(createdContext, Is.Not.Null);
        var closeException = Assert.CatchAsync(async () => await createdContext!.NewPageAsync());
        Assert.That(closeException!.GetType().Name, Is.EqualTo("TargetClosedException"));
    }

    private static ExternalBrowserScenarioLeaseOptions CreateScenarioOptions(
        Uri webBaseUri,
        string diagnosticsPath,
        IReadOnlyList<string>? sensitiveValues = null) =>
        new(
            webBaseUri,
            OperationTimeout,
            OperationTimeout,
            diagnosticsPath,
            sensitiveValues ?? []);

    private static ExternalBrowserRunLeaseOptions CreateRunOptions() => new(OperationTimeout, "chrome");

    private static async Task<ObservedBrowserState> ReadStateAsync(IPage page)
    {
        var json = await page.EvaluateAsync<string>("""
            async () => JSON.stringify({
                cookie: document.cookie,
                localStorage: Object.keys(localStorage),
                sessionStorage: Object.keys(sessionStorage),
                indexedDatabases: (await indexedDB.databases()).map(database => database.name),
                caches: await caches.keys(),
                serviceWorkers: (await navigator.serviceWorker.getRegistrations()).map(registration => registration.scope)
            })
            """);

        return JsonSerializer.Deserialize<ObservedBrowserState>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static bool DoesNotContainCanary(string artifactPath, params string[] canaries)
    {
        var content = File.ReadAllBytes(artifactPath);
        return canaries.All(canary => content.AsSpan().IndexOf(Encoding.UTF8.GetBytes(canary)) < 0);
    }

    private static string NewDiagnosticsPath() => Path.Combine(
        Path.GetTempPath(),
        "lgym-external-e2e-browser-lease",
        Guid.NewGuid().ToString("N"));

    private sealed record ObservedBrowserState(
        string Cookie,
        IReadOnlyList<string> LocalStorage,
        IReadOnlyList<string> SessionStorage,
        IReadOnlyList<string> IndexedDatabases,
        IReadOnlyList<string> Caches,
        IReadOnlyList<string> ServiceWorkers);
}
