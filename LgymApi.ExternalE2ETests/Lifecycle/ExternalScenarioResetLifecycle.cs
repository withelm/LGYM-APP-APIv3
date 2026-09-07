using System.Data.Common;
using System.Text.RegularExpressions;
using LgymApi.ExternalE2ETests.Browser;
using LgymApi.ExternalE2ETests.Configuration;
using LgymApi.ExternalE2ETests.PostgreSql;
using Microsoft.Playwright;

namespace LgymApi.ExternalE2ETests.Lifecycle;

internal interface IExternalScenarioOptionsSource
{
    ExternalE2EOptions Load();
}

internal interface IExternalScenarioRestoreGateway
{
    ValueTask RestoreAsync(ExternalE2EOptions options, CancellationToken cancellationToken);
}

internal interface IExternalScenarioBrowserLease : IAsyncDisposable
{
    void MarkFailed();
}

internal interface IExternalScenarioBrowserPageLease
{
    IPage Page { get; }
}

internal interface IExternalScenarioBrowserGateway : IAsyncDisposable
{
    ValueTask<IExternalScenarioBrowserLease> AcquireAsync(
        ExternalE2EOptions options,
        string scenarioId,
        CancellationToken cancellationToken);
}

internal sealed class ExternalScenarioResetLifecycle : IAsyncDisposable
{
    private readonly IExternalScenarioApiRecoveryGateway _apiRecovery;
    private readonly IExternalScenarioBrowserGateway _browser;
    private readonly IExternalScenarioOptionsSource _optionsSource;
    private readonly IExternalScenarioRestoreGateway _restore;
    private readonly SemaphoreSlim _scenarioGate = new(1, 1);

    public ExternalScenarioResetLifecycle(
        IExternalScenarioOptionsSource optionsSource,
        IExternalScenarioRestoreGateway restore,
        IExternalScenarioApiRecoveryGateway apiRecovery,
        IExternalScenarioBrowserGateway browser)
    {
        _optionsSource = optionsSource ?? throw new ArgumentNullException(nameof(optionsSource));
        _restore = restore ?? throw new ArgumentNullException(nameof(restore));
        _apiRecovery = apiRecovery ?? throw new ArgumentNullException(nameof(apiRecovery));
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
    }

    public async ValueTask<ExternalScenarioPreparedLease> PrepareAsync(string scenarioId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            throw new ArgumentException("A scenario identifier is required.", nameof(scenarioId));
        }

        await _scenarioGate.WaitAsync(cancellationToken);
        try
        {
            var options = _optionsSource.Load();
            await _restore.RestoreAsync(options, cancellationToken);
            await _apiRecovery.WaitForRecoveryAsync(options, cancellationToken);
            var browserLease = await _browser.AcquireAsync(options, scenarioId, cancellationToken);
            return new ExternalScenarioPreparedLease(browserLease, options);
        }
        catch
        {
            _scenarioGate.Release();
            throw;
        }
    }

    public async ValueTask CompleteAsync(ExternalScenarioPreparedLease lease, bool failed)
    {
        ArgumentNullException.ThrowIfNull(lease);

        try
        {
            if (failed)
            {
                lease.BrowserLease.MarkFailed();
            }

            await lease.BrowserLease.DisposeAsync();
        }
        finally
        {
            _scenarioGate.Release();
        }
    }

    public ValueTask DisposeAsync() => _browser.DisposeAsync();
}

internal sealed class ExternalScenarioPreparedLease(
    IExternalScenarioBrowserLease browserLease,
    ExternalE2EOptions options)
{
    internal IExternalScenarioBrowserLease BrowserLease { get; } = browserLease;

    internal ExternalE2EOptions Options { get; } = options;

    internal IPage Page => BrowserLease is IExternalScenarioBrowserPageLease pageLease
        ? pageLease.Page
        : throw new InvalidOperationException("External scenario preparation did not provide a browser page.");
}

internal sealed class FileExternalScenarioOptionsSource(string configurationPath) : IExternalScenarioOptionsSource
{
    public ExternalE2EOptions Load() => ExternalE2EOptionsLoader.Load(configurationPath);
}

internal sealed class ExternalPostgreSqlRestoreGateway : IExternalScenarioRestoreGateway
{
    private readonly PostgreSqlRestoreProtocol _protocol;

    public ExternalPostgreSqlRestoreGateway(IPostgreSqlToolRunner runner)
    {
        _protocol = new PostgreSqlRestoreProtocol(runner);
    }

    public async ValueTask RestoreAsync(ExternalE2EOptions options, CancellationToken cancellationToken)
    {
        var request = CreateRequest(options);
        var result = await _protocol.RestoreAsync(request, cancellationToken);
        if (!result.IsHealthy)
        {
            throw new InvalidOperationException($"External scenario restore failed at stage {result.Failure!.Stage}.");
        }
    }

    private static PostgreSqlRestoreRequest CreateRequest(ExternalE2EOptions options)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = options.ConnectionString };
        var port = ReadPort(builder);
        var password = ReadPassword(builder);
        return new PostgreSqlRestoreRequest(
            new PostgreSqlTarget(
                options.ConnectionIdentity.Host,
                port,
                options.ConnectionIdentity.DatabaseName,
                options.ConnectionIdentity.UserName,
                new PostgreSqlCredential(password)),
            new PostgreSqlExpectedIdentity(
                options.ConnectionIdentity.Host,
                options.ExpectedDatabaseName,
                options.ExpectedDatabaseMarker),
            options.BaselineDumpPath,
            options.RestoreTimeout,
            4096);
    }

    private static int ReadPort(DbConnectionStringBuilder builder)
    {
        if (!builder.TryGetValue("Port", out var value))
        {
            return 5432;
        }

        return int.TryParse(value?.ToString(), out var port) && port is >= 1 and <= 65535
            ? port
            : throw new InvalidOperationException("External scenario restore configuration is invalid: port.");
    }

    private static string ReadPassword(DbConnectionStringBuilder builder) =>
        builder.TryGetValue("Password", out var value) && value is string password && !string.IsNullOrWhiteSpace(password)
            ? password
            : throw new InvalidOperationException("External scenario restore configuration is invalid: password.");
}

internal sealed class ExternalBrowserScenarioGateway : IExternalScenarioBrowserGateway
{
    private static readonly Regex ScenarioIdPattern = new("^[A-Z0-9-]+$", RegexOptions.CultureInvariant);
    private ExternalBrowserRunLease? _runLease;

    public async ValueTask<IExternalScenarioBrowserLease> AcquireAsync(
        ExternalE2EOptions options,
        string scenarioId,
        CancellationToken cancellationToken)
    {
        if (!ScenarioIdPattern.IsMatch(scenarioId))
        {
            throw new InvalidOperationException("External scenario identifier is invalid.");
        }

        _runLease ??= await ExternalBrowserRunLease.CreateAsync(
            new ExternalBrowserRunLeaseOptions(options.BrowserTimeout),
            cancellationToken);
        var diagnosticsDirectory = Path.Combine(options.ArtifactRoot, scenarioId);
        var lease = await _runLease.AcquireScenarioAsync(
            new ExternalBrowserScenarioLeaseOptions(
                options.WebBaseUrl,
                options.BrowserTimeout,
                options.BrowserTimeout,
                diagnosticsDirectory,
                [options.ConnectionString]),
            cancellationToken);
        return new ExternalScenarioBrowserLeaseAdapter(lease);
    }

    public async ValueTask DisposeAsync()
    {
        var runLease = Interlocked.Exchange(ref _runLease, null);
        if (runLease is not null)
        {
            await runLease.DisposeAsync();
        }
    }

    private sealed class ExternalScenarioBrowserLeaseAdapter(ExternalBrowserScenarioLease lease)
        : IExternalScenarioBrowserLease, IExternalScenarioBrowserPageLease
    {
        public IPage Page => lease.Page;

        public void MarkFailed() => lease.MarkFailed();

        public ValueTask DisposeAsync() => lease.DisposeAsync();
    }
}
