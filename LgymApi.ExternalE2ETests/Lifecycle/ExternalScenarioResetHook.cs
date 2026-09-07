using Reqnroll;
using LgymApi.ExternalE2ETests.PostgreSql;

namespace LgymApi.ExternalE2ETests.Lifecycle;

[Binding]
public sealed class ExternalScenarioResetHook
{
    internal const string PreparedLeaseKey = "external-scenario-prepared-lease";
    private static readonly ExternalScenarioResetLifecycle Lifecycle = CreateLifecycle();
    private readonly ScenarioContext _scenarioContext;

    public ExternalScenarioResetHook(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
    }

    [BeforeScenario("external", Order = 0)]
    public async Task ResetExternalScenarioAsync()
    {
        var scenarioId = _scenarioContext.ScenarioInfo.Title.Split(':', 2)[0].Trim();
        var lease = await Lifecycle.PrepareAsync(scenarioId, CancellationToken.None);
        _scenarioContext[PreparedLeaseKey] = lease;
    }

    [AfterScenario("external", Order = 0)]
    public async Task DisposeExternalScenarioAsync()
    {
        if (_scenarioContext.TryGetValue(PreparedLeaseKey, out ExternalScenarioPreparedLease? lease) && lease is not null)
        {
            try
            {
                await Lifecycle.CompleteAsync(lease, _scenarioContext.TestError is not null);
            }
            catch when (_scenarioContext.TestError is not null)
            {
            }
        }
    }

    [AfterTestRun(Order = 0)]
    public static async Task DisposeExternalBrowserRunAsync()
    {
        await Lifecycle.DisposeAsync();
    }

    internal static ExternalScenarioPreparedLease GetPreparedLease(ScenarioContext scenarioContext)
    {
        ArgumentNullException.ThrowIfNull(scenarioContext);
        return scenarioContext.TryGetValue(PreparedLeaseKey, out ExternalScenarioPreparedLease? lease) && lease is not null
            ? lease
            : throw new InvalidOperationException("External scenario preparation did not provide a browser lease.");
    }

    private static ExternalScenarioResetLifecycle CreateLifecycle()
    {
        var configurationPath = Path.Combine(Environment.CurrentDirectory, "appsettings.ExternalE2E.json");
        var recovery = new ExternalApiRecoveryGate(new HttpClientHandler { AllowAutoRedirect = false }, new SystemExternalScenarioClock());
        return new ExternalScenarioResetLifecycle(
            new FileExternalScenarioOptionsSource(configurationPath),
            new ExternalPostgreSqlRestoreGateway(new SystemPostgreSqlToolRunner()),
            recovery,
            new ExternalBrowserScenarioGateway());
    }
}
