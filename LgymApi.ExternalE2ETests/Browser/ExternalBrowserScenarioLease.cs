using System.Text;
using Microsoft.Playwright;

namespace LgymApi.ExternalE2ETests.Browser;

public sealed class ExternalBrowserScenarioLease : IAsyncDisposable
{
    public const long MaxDiagnosticArtifactBytes = 2 * 1024 * 1024;

    private readonly List<string> _artifactFileNames = [];
    private readonly List<string> _cleanupFailures = [];
    private readonly IBrowserContext _context;
    private readonly ExternalBrowserScenarioLeaseOptions _options;
    private bool _contextClosed;
    private bool _disposed;
    private bool _failed;
    private bool _traceStarted;
    private bool _traceStopped;

    private ExternalBrowserScenarioLease(
        IBrowserContext context,
        IPage page,
        ExternalBrowserScenarioLeaseOptions options)
    {
        _context = context;
        Page = page;
        _options = options;
    }

    public IPage Page { get; }

    public ExternalBrowserScenarioCleanupReceipt CleanupReceipt => new(
        _failed,
        _contextClosed,
        _traceStopped,
        _artifactFileNames.AsReadOnly(),
        _cleanupFailures.AsReadOnly());

    internal static async Task<ExternalBrowserScenarioLease> CreateAsync(
        IBrowser browser,
        ExternalBrowserScenarioLeaseOptions options,
        CancellationToken cancellationToken,
        Func<IBrowserContext, Task<IPage>>? pageFactory)
    {
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        IBrowserContext? context = null;

        try
        {
            context = await BrowserOperation.AwaitAsync(
                browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = options.WebBaseUri.AbsoluteUri }),
                options.OperationTimeout,
                cancellationToken,
                static lateContext => lateContext.CloseAsync());
            context.SetDefaultTimeout((float)options.OperationTimeout.TotalMilliseconds);

            var createPage = pageFactory ?? (static browserContext => browserContext.NewPageAsync());
            var page = await BrowserOperation.AwaitAsync(
                createPage(context),
                options.OperationTimeout,
                cancellationToken);
            page.SetDefaultNavigationTimeout((float)options.NavigationTimeout.TotalMilliseconds);
            await BrowserOperation.AwaitAsync(
                context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true }),
                options.OperationTimeout,
                cancellationToken);

            var lease = new ExternalBrowserScenarioLease(context, page, options)
            {
                _traceStarted = true
            };
            context = null;
            return lease;
        }
        catch
        {
            if (context is not null)
            {
                try
                {
                    await BrowserOperation.AwaitAsync(
                        context.CloseAsync(),
                        options.OperationTimeout,
                        CancellationToken.None);
                }
                catch
                {
                }
            }

            throw;
        }
    }

    public void MarkFailed() => _failed = true;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (_failed)
            {
                await SaveFailureDiagnosticsAsync();
            }
            else
            {
                await StopTracingAsync(null);
                DeleteDiagnosticsDirectory();
            }
        }
        finally
        {
            await CloseContextAsync();
        }
    }

    private async Task SaveFailureDiagnosticsAsync()
    {
        try
        {
            Directory.CreateDirectory(_options.PrivateDiagnosticsDirectory);
            await CaptureScreenshotAsync();
            await StopTracingAsync(Path.Combine(_options.PrivateDiagnosticsDirectory, "trace.zip"));
        }
        catch
        {
            _cleanupFailures.Add("diagnostics");
        }
    }

    private async Task CaptureScreenshotAsync()
    {
        var screenshotPath = Path.Combine(_options.PrivateDiagnosticsDirectory, "screenshot.jpg");

        try
        {
            await BrowserOperation.AwaitAsync(
                Page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = screenshotPath,
                    Type = ScreenshotType.Jpeg,
                    Quality = 50
                }),
                _options.OperationTimeout,
                CancellationToken.None);
            RetainSanitizedArtifact(screenshotPath);
        }
        catch
        {
            _cleanupFailures.Add("screenshot");
        }
    }

    private async Task StopTracingAsync(string? tracePath)
    {
        if (!_traceStarted)
        {
            return;
        }

        try
        {
            await BrowserOperation.AwaitAsync(
                _context.Tracing.StopAsync(tracePath is null ? null : new TracingStopOptions { Path = tracePath }),
                _options.OperationTimeout,
                CancellationToken.None);
            _traceStopped = true;
            _traceStarted = false;

            if (tracePath is not null)
            {
                RetainSanitizedArtifact(tracePath);
            }
        }
        catch
        {
            _cleanupFailures.Add("trace");
        }
    }

    private void RetainSanitizedArtifact(string artifactPath)
    {
        if (!File.Exists(artifactPath))
        {
            return;
        }

        var artifact = new FileInfo(artifactPath);
        if (artifact.Length > MaxDiagnosticArtifactBytes || ContainsSensitiveValue(artifactPath))
        {
            File.Delete(artifactPath);
            return;
        }

        _artifactFileNames.Add(Path.GetFileName(artifactPath));
    }

    private bool ContainsSensitiveValue(string artifactPath)
    {
        var content = File.ReadAllBytes(artifactPath);
        return _options.SensitiveValues.Any(value =>
            !string.IsNullOrWhiteSpace(value) && content.AsSpan().IndexOf(Encoding.UTF8.GetBytes(value)) >= 0);
    }

    private async Task CloseContextAsync()
    {
        try
        {
            await BrowserOperation.AwaitAsync(
                _context.CloseAsync(),
                _options.OperationTimeout,
                CancellationToken.None);
            _contextClosed = true;
        }
        catch
        {
            _cleanupFailures.Add("context");
        }
    }

    private void DeleteDiagnosticsDirectory()
    {
        if (Directory.Exists(_options.PrivateDiagnosticsDirectory))
        {
            Directory.Delete(_options.PrivateDiagnosticsDirectory, true);
        }
    }
}

public sealed record ExternalBrowserScenarioLeaseOptions(
    Uri WebBaseUri,
    TimeSpan OperationTimeout,
    TimeSpan NavigationTimeout,
    string PrivateDiagnosticsDirectory,
    IReadOnlyList<string> SensitiveValues)
{
    internal void Validate()
    {
        if ((WebBaseUri.Scheme != Uri.UriSchemeHttp && WebBaseUri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(WebBaseUri.UserInfo))
        {
            throw new ArgumentException("The external web base URL must be an HTTP(S) URL without user information.", nameof(WebBaseUri));
        }

        if (OperationTimeout <= TimeSpan.Zero || NavigationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(OperationTimeout));
        }

        if (!Path.IsPathFullyQualified(PrivateDiagnosticsDirectory))
        {
            throw new ArgumentException("The private diagnostics directory must be absolute.", nameof(PrivateDiagnosticsDirectory));
        }
    }
}

public sealed record ExternalBrowserScenarioCleanupReceipt(
    bool Failed,
    bool ContextClosed,
    bool TraceStopped,
    IReadOnlyList<string> ArtifactFileNames,
    IReadOnlyList<string> CleanupFailureStages);
