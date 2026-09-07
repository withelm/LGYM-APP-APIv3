using Microsoft.Playwright;

namespace LgymApi.ExternalE2ETests.Browser;

public sealed class ExternalBrowserRunLease : IAsyncDisposable
{
    private IBrowser? _browser;
    private IPlaywright? _playwright;

    private ExternalBrowserRunLease(IPlaywright playwright, IBrowser browser, ExternalBrowserRunLeaseOptions options)
    {
        _playwright = playwright;
        _browser = browser;
        Options = options;
    }

    public ExternalBrowserRunLeaseOptions Options { get; }

    public static async Task<ExternalBrowserRunLease> CreateAsync(
        ExternalBrowserRunLeaseOptions options,
        CancellationToken cancellationToken)
    {
        options.Validate();
        IPlaywright? playwright = null;
        IBrowser? browser = null;

        try
        {
            playwright = await BrowserOperation.AwaitAsync(
                Playwright.CreateAsync(),
                options.LaunchTimeout,
                cancellationToken,
                static latePlaywright =>
                {
                    latePlaywright.Dispose();
                    return Task.CompletedTask;
                });
            browser = await BrowserOperation.AwaitAsync(
                playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true,
                    Channel = options.BrowserChannel
                }),
                options.LaunchTimeout,
                cancellationToken,
                static lateBrowser => lateBrowser.CloseAsync());

            return new ExternalBrowserRunLease(playwright, browser, options);
        }
        catch
        {
            if (browser is not null)
            {
                try
                {
                    await BrowserOperation.AwaitAsync(browser.CloseAsync(), options.LaunchTimeout, CancellationToken.None);
                }
                catch
                {
                }
            }

            playwright?.Dispose();
            throw;
        }
    }

    public Task<ExternalBrowserScenarioLease> AcquireScenarioAsync(
        ExternalBrowserScenarioLeaseOptions options,
        CancellationToken cancellationToken,
        Func<IBrowserContext, Task<IPage>>? pageFactory = null)
    {
        var browser = _browser ?? throw new ObjectDisposedException(nameof(ExternalBrowserRunLease));
        return ExternalBrowserScenarioLease.CreateAsync(browser, options, cancellationToken, pageFactory);
    }

    public async ValueTask DisposeAsync()
    {
        var browser = Interlocked.Exchange(ref _browser, null);
        var playwright = Interlocked.Exchange(ref _playwright, null);

        try
        {
            if (browser is not null)
            {
                await BrowserOperation.AwaitAsync(browser.CloseAsync(), Options.LaunchTimeout, CancellationToken.None);
            }
        }
        finally
        {
            playwright?.Dispose();
        }
    }
}

public sealed record ExternalBrowserRunLeaseOptions(TimeSpan LaunchTimeout, string? BrowserChannel = null)
{
    internal void Validate()
    {
        if (LaunchTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(LaunchTimeout));
        }
    }
}

internal static class BrowserOperation
{
    public static async Task AwaitAsync(Task operation, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCancellationSource = new CancellationTokenSource(timeout);
        using var linkedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellationSource.Token);

        await operation.WaitAsync(linkedCancellationSource.Token);
    }

    public static async Task<T> AwaitAsync<T>(
        Task<T> operation,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Func<T, Task>? disposeLateResult = null)
    {
        using var timeoutCancellationSource = new CancellationTokenSource(timeout);
        using var linkedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellationSource.Token);

        try
        {
            return await operation.WaitAsync(linkedCancellationSource.Token);
        }
        catch
        {
            if (disposeLateResult is not null)
            {
                _ = DisposeLateResultAsync(operation, disposeLateResult);
            }

            throw;
        }
    }

    private static async Task DisposeLateResultAsync<T>(Task<T> operation, Func<T, Task> disposeLateResult)
    {
        try
        {
            await disposeLateResult(await operation);
        }
        catch
        {
        }
    }
}
