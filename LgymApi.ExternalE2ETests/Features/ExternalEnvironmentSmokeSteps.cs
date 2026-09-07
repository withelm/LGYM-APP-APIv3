using LgymApi.ExternalE2ETests.Lifecycle;
using Microsoft.Playwright;
using Reqnroll;

namespace LgymApi.ExternalE2ETests.Features;

[Binding]
public sealed class ExternalEnvironmentSmokeSteps
{
    private readonly ScenarioContext _scenarioContext;
    private ExternalEnvironmentSmokeProbe? _probe;

    public ExternalEnvironmentSmokeSteps(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
    }

    [Given("the baseline dump was restored and the external API recovered")]
    public void GivenTheBaselineDumpWasRestoredAndTheExternalApiRecovered()
    {
        var lease = ExternalScenarioResetHook.GetPreparedLease(_scenarioContext);
        _probe = new ExternalEnvironmentSmokeProbe(
            lease.Page,
            lease.Options.WebBaseUrl,
            lease.Options.ApiBaseUrl,
            lease.Options.BrowserTimeout);
    }

    [When("the browser opens the configured web URL")]
    public Task WhenTheBrowserOpensTheConfiguredWebUrl() => RequireProbe().NavigateAsync();

    [Then("a deterministic unauthenticated application surface is visible")]
    public Task ThenADeterministicUnauthenticatedApplicationSurfaceIsVisible() =>
        RequireProbe().RequireUnauthenticatedSurfaceAsync();

    [Then("an application request targets the configured API origin")]
    public Task ThenAnApplicationRequestTargetsTheConfiguredApiOrigin() =>
        RequireProbe().RequireConfiguredApiRequestAsync();

    private ExternalEnvironmentSmokeProbe RequireProbe() => _probe ??
        throw new InvalidOperationException("External environment smoke navigation has not started.");
}

internal sealed class ExternalEnvironmentSmokeProbe
{
    private readonly Uri _apiBaseUrl;
    private readonly TimeSpan _timeout;
    private readonly Uri _webBaseUrl;
    private TaskCompletionSource? _configuredApiRequest;
    private EventHandler<IRequest>? _requestHandler;

    internal ExternalEnvironmentSmokeProbe(IPage page, Uri webBaseUrl, Uri apiBaseUrl, TimeSpan timeout)
    {
        Page = page ?? throw new ArgumentNullException(nameof(page));
        _webBaseUrl = webBaseUrl ?? throw new ArgumentNullException(nameof(webBaseUrl));
        _apiBaseUrl = apiBaseUrl ?? throw new ArgumentNullException(nameof(apiBaseUrl));
        _timeout = timeout > TimeSpan.Zero ? timeout : throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    private IPage Page { get; }

    internal async Task NavigateAsync()
    {
        _configuredApiRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _requestHandler = OnRequest;
        Page.Request += _requestHandler;

        try
        {
            var response = await Page.GotoAsync(_webBaseUrl.AbsoluteUri);
            if (response?.Request.RedirectedFrom is not null ||
                !Uri.TryCreate(Page.Url, UriKind.Absolute, out var currentUri) ||
                !HasSameOrigin(currentUri, _webBaseUrl))
            {
                throw new InvalidOperationException("Configured web navigation redirected away from its configured origin.");
            }
        }
        catch
        {
            DetachRequestListener();
            throw;
        }
    }

    internal async Task RequireUnauthenticatedSurfaceAsync()
    {
        try
        {
            await Page.GetByRole(AriaRole.Main).WaitForAsync(
                new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = (float)_timeout.TotalMilliseconds });
        }
        catch
        {
            DetachRequestListener();
            throw;
        }
    }

    internal async Task RequireConfiguredApiRequestAsync()
    {
        try
        {
            var request = _configuredApiRequest ?? throw new InvalidOperationException(
                "External environment smoke navigation has not started.");
            await request.Task.WaitAsync(_timeout);
        }
        finally
        {
            DetachRequestListener();
        }
    }

    private void OnRequest(object? sender, IRequest request)
    {
        if (request.ResourceType is not "fetch" and not "xhr")
        {
            return;
        }

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var requestUri) || !HasSameOrigin(requestUri, _apiBaseUrl))
        {
            _configuredApiRequest?.TrySetException(new InvalidOperationException(
                "An application request targeted an origin other than the configured API origin."));
            return;
        }

        _configuredApiRequest?.TrySetResult();
    }

    private void DetachRequestListener()
    {
        if (_requestHandler is not null)
        {
            Page.Request -= _requestHandler;
            _requestHandler = null;
        }
    }

    private static bool HasSameOrigin(Uri actual, Uri expected) => Uri.Compare(
        actual,
        expected,
        UriComponents.SchemeAndServer,
        UriFormat.Unescaped,
        StringComparison.OrdinalIgnoreCase) == 0;
}
