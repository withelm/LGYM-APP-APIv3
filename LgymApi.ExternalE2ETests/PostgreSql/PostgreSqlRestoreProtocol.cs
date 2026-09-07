using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace LgymApi.ExternalE2ETests.PostgreSql;

internal sealed record PostgreSqlTarget(
    string Host,
    int Port,
    string DatabaseName,
    string Username,
    PostgreSqlCredential Credential);

internal sealed record PostgreSqlExpectedIdentity(string Host, string DatabaseName, string Marker);

internal sealed record PostgreSqlRestoreRequest(
    PostgreSqlTarget Target,
    PostgreSqlExpectedIdentity ExpectedIdentity,
    string BaselineDumpPath,
    TimeSpan ToolTimeout,
    int OutputLimit);

internal sealed record PostgreSqlRestoreFailure(
    PostgreSqlRestoreStage Stage,
    int? ExitCode,
    string SanitizedDiagnostic);

internal sealed record PostgreSqlRestoreResult(
    bool IsHealthy,
    PostgreSqlRestoreFailure? Failure,
    ImmutableArray<string> CommandReceipts);

internal sealed class PostgreSqlRestoreValidationException : InvalidOperationException
{
    public PostgreSqlRestoreValidationException()
        : base("External PostgreSQL restore configuration was rejected before PostgreSQL tools were invoked. Connection values are redacted.")
    {
    }
}

internal sealed record PostgreSqlSessionFixture(string DatabaseName, int ProcessId);

internal static class PostgreSqlTargetSessionScope
{
    public static bool ShouldTerminate(
        PostgreSqlSessionFixture session,
        string targetDatabaseName,
        int currentProcessId) =>
        string.Equals(session.DatabaseName, targetDatabaseName, StringComparison.Ordinal) &&
        session.ProcessId != currentProcessId;
}

internal sealed class PostgreSqlRestoreProtocol
{
    private const string MarkerQuery =
        "SELECT marker FROM public.external_e2e_restore_marker WHERE marker = :'expected_marker';";

    private const string QuiescenceQuery = """
        WITH terminated AS (
            SELECT pg_terminate_backend(pid) AS terminated
            FROM pg_stat_activity
            WHERE datname = current_database()
              AND datname = :'expected_database'
              AND pid <> pg_backend_pid()
        )
        SELECT COALESCE(bool_and(terminated), TRUE) FROM terminated;
        """;

    private static readonly Regex DedicatedDatabaseName = new(
        "^lgym_external_e2e(?:_[a-z0-9]+)*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex MarkerFormat = new(
        "^lgym_external_e2e_v[1-9][0-9]*$",
        RegexOptions.CultureInvariant);

    private readonly IPostgreSqlToolRunner _runner;

    public PostgreSqlRestoreProtocol(IPostgreSqlToolRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public async ValueTask<PostgreSqlRestoreResult> RestoreAsync(
        PostgreSqlRestoreRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        var receipts = ImmutableArray.CreateBuilder<string>();

        var psqlDiscovery = await RunAsync(CreateDiscoveryCommand(PostgreSqlTool.Psql, request), null, request, receipts, cancellationToken);
        if (psqlDiscovery.Failure is not null)
        {
            return Unhealthy(psqlDiscovery.Failure, receipts);
        }

        var pgRestoreDiscovery = await RunAsync(CreateDiscoveryCommand(PostgreSqlTool.PgRestore, request), null, request, receipts, cancellationToken);
        if (pgRestoreDiscovery.Failure is not null)
        {
            return Unhealthy(pgRestoreDiscovery.Failure, receipts);
        }

        var dumpList = await RunAsync(CreateDumpListCommand(request), null, request, receipts, cancellationToken);
        if (dumpList.Failure is not null)
        {
            return Unhealthy(dumpList.Failure, receipts);
        }

        if (!ContainsTableOfContentsEntry(dumpList.Result!.OutputChunks))
        {
            return Unhealthy(Failure(PostgreSqlRestoreStage.ListDump, dumpList.Result!, "The baseline dump did not produce a valid archive table of contents."), receipts);
        }

        var preRestoreMarker = await RunAsync(CreateMarkerCommand(request, PostgreSqlRestoreStage.PreRestoreMarker), request.Target.Credential, request, receipts, cancellationToken);
        if (preRestoreMarker.Failure is not null)
        {
            return Unhealthy(preRestoreMarker.Failure, receipts);
        }

        if (!OutputEquals(preRestoreMarker.Result!, request.ExpectedIdentity.Marker))
        {
            return Unhealthy(Failure(PostgreSqlRestoreStage.PreRestoreMarker, preRestoreMarker.Result!, "The validated target marker was not present before restore."), receipts);
        }

        var quiescence = await RunAsync(CreateQuiescenceCommand(request), request.Target.Credential, request, receipts, cancellationToken);
        if (quiescence.Failure is not null)
        {
            return Unhealthy(quiescence.Failure, receipts);
        }

        if (!OutputEquals(quiescence.Result!, "t"))
        {
            return Unhealthy(Failure(PostgreSqlRestoreStage.QuiesceTarget, quiescence.Result!, "One or more exact-target peer sessions could not be terminated."), receipts);
        }

        var restore = await RunAsync(CreateRestoreCommand(request), request.Target.Credential, request, receipts, cancellationToken);
        if (restore.Failure is not null)
        {
            return Unhealthy(restore.Failure, receipts);
        }

        var postRestoreMarker = await RunAsync(CreateMarkerCommand(request, PostgreSqlRestoreStage.PostRestoreMarker), request.Target.Credential, request, receipts, cancellationToken);
        if (postRestoreMarker.Failure is not null)
        {
            return Unhealthy(postRestoreMarker.Failure, receipts);
        }

        if (!OutputEquals(postRestoreMarker.Result!, request.ExpectedIdentity.Marker))
        {
            return Unhealthy(Failure(PostgreSqlRestoreStage.PostRestoreMarker, postRestoreMarker.Result!, "The restored target marker is missing; the target is unhealthy."), receipts);
        }

        return new PostgreSqlRestoreResult(true, null, receipts.ToImmutable());
    }

    private async ValueTask<StepResult> RunAsync(
        PostgreSqlToolCommand command,
        PostgreSqlCredential? credential,
        PostgreSqlRestoreRequest request,
        ImmutableArray<string>.Builder receipts,
        CancellationToken cancellationToken)
    {
        receipts.Add(command.SafeReceipt);

        try
        {
            var result = await _runner.RunAsync(command, credential, cancellationToken);
            if (result.TimedOut)
            {
                return new StepResult(result, Failure(command.Stage, result, "The PostgreSQL tool timed out."));
            }

            if (result.ExitCode != 0)
            {
                return new StepResult(result, Failure(command.Stage, result, Sanitize(result.OutputChunks, request)));
            }

            return new StepResult(result, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new StepResult(
                null,
                new PostgreSqlRestoreFailure(command.Stage, null, Sanitize([exception.Message], request)));
        }
    }

    private static void Validate(PostgreSqlRestoreRequest request)
    {
        if (request is null ||
            request.Target is null ||
            request.ExpectedIdentity is null ||
            request.Target.Credential is null ||
            string.IsNullOrWhiteSpace(request.Target.Credential.Reveal()) ||
            request.Target.Port is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(request.Target.Username) ||
            Uri.CheckHostName(request.Target.Host) == UriHostNameType.Unknown ||
            !string.Equals(request.Target.Host, request.ExpectedIdentity.Host, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.Target.DatabaseName, request.ExpectedIdentity.DatabaseName, StringComparison.Ordinal) ||
            !DedicatedDatabaseName.IsMatch(request.Target.DatabaseName) ||
            !MarkerFormat.IsMatch(request.ExpectedIdentity.Marker) ||
            !Path.IsPathFullyQualified(request.BaselineDumpPath) ||
            !string.Equals(Path.GetExtension(request.BaselineDumpPath), ".bak", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(request.BaselineDumpPath) ||
            request.ToolTimeout < TimeSpan.FromSeconds(1) ||
            request.ToolTimeout > TimeSpan.FromMinutes(5) ||
            request.OutputLimit is < 256 or > 65536)
        {
            throw new PostgreSqlRestoreValidationException();
        }
    }

    private static PostgreSqlToolCommand CreateDiscoveryCommand(PostgreSqlTool tool, PostgreSqlRestoreRequest request)
    {
        var stage = tool == PostgreSqlTool.Psql
            ? PostgreSqlRestoreStage.DiscoverPsql
            : PostgreSqlRestoreStage.DiscoverPgRestore;
        var name = tool == PostgreSqlTool.Psql ? "psql" : "pg_restore";
        return new PostgreSqlToolCommand(
            tool,
            stage,
            ["--version"],
            null,
            request.ToolTimeout,
            request.OutputLimit,
            $"{stage.ToString().ToLowerInvariant()}: {name} --version");
    }

    private static PostgreSqlToolCommand CreateDumpListCommand(PostgreSqlRestoreRequest request) => new(
        PostgreSqlTool.PgRestore,
        PostgreSqlRestoreStage.ListDump,
        ["--list", request.BaselineDumpPath],
        null,
        request.ToolTimeout,
        request.OutputLimit,
        "list-dump: pg_restore --list <baseline-dump>");

    private static PostgreSqlToolCommand CreateMarkerCommand(
        PostgreSqlRestoreRequest request,
        PostgreSqlRestoreStage stage) => new(
        PostgreSqlTool.Psql,
        stage,
        PsqlArguments(request, "expected_marker", request.ExpectedIdentity.Marker),
        MarkerQuery,
        request.ToolTimeout,
        CredentialAwareCaptureLimit(request),
        $"{(stage == PostgreSqlRestoreStage.PreRestoreMarker ? "pre-restore-marker" : "post-restore-marker")}: psql <validated-target> <fixed-query>");

    private static PostgreSqlToolCommand CreateQuiescenceCommand(PostgreSqlRestoreRequest request) => new(
        PostgreSqlTool.Psql,
        PostgreSqlRestoreStage.QuiesceTarget,
        PsqlArguments(request, "expected_database", request.ExpectedIdentity.DatabaseName),
        QuiescenceQuery,
        request.ToolTimeout,
        CredentialAwareCaptureLimit(request),
        "quiesce-target: psql <validated-target> <fixed-query>");

    private static PostgreSqlToolCommand CreateRestoreCommand(PostgreSqlRestoreRequest request) => new(
        PostgreSqlTool.PgRestore,
        PostgreSqlRestoreStage.Restore,
        [
            "--clean",
            "--if-exists",
            "--no-owner",
            "--no-privileges",
            "--exit-on-error",
            "--host", request.Target.Host,
            "--port", request.Target.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--username", request.Target.Username,
            "--dbname", request.Target.DatabaseName,
            request.BaselineDumpPath
        ],
        null,
        request.ToolTimeout,
        CredentialAwareCaptureLimit(request),
        "restore: pg_restore --clean --if-exists --no-owner --no-privileges --exit-on-error <validated-target> <baseline-dump>");

    private static ImmutableArray<string> PsqlArguments(
        PostgreSqlRestoreRequest request,
        string variableName,
        string variableValue) =>
        [
            "--no-psqlrc",
            "--quiet",
            "--tuples-only",
            "--no-align",
            "--set", "ON_ERROR_STOP=on",
            "--set", $"{variableName}={variableValue}",
            "--host", request.Target.Host,
            "--port", request.Target.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--username", request.Target.Username,
            "--dbname", request.Target.DatabaseName
        ];

    private static bool ContainsTableOfContentsEntry(ImmutableArray<string> chunks)
    {
        var lines = string.Concat(chunks).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return lines.Any(line =>
        {
            var separator = line.IndexOf(';');
            return separator > 0 && line[..separator].All(char.IsDigit);
        });
    }

    private static bool OutputEquals(PostgreSqlToolResult result, string expected) =>
        string.Equals(string.Concat(result.OutputChunks).Trim(), expected, StringComparison.Ordinal);

    private static PostgreSqlRestoreResult Unhealthy(
        PostgreSqlRestoreFailure failure,
        ImmutableArray<string>.Builder receipts) =>
        new(false, failure, receipts.ToImmutable());

    private static PostgreSqlRestoreFailure Failure(
        PostgreSqlRestoreStage stage,
        PostgreSqlToolResult result,
        string diagnostic) =>
        new(stage, result.ExitCode, diagnostic);

    private static string Sanitize(IEnumerable<string> chunks, PostgreSqlRestoreRequest request)
    {
        var secret = request.Target.Credential.Reveal();
        var captureLimit = request.OutputLimit + secret.Length;
        var output = new StringBuilder(Math.Min(captureLimit, 4096));

        foreach (var chunk in chunks)
        {
            var remaining = captureLimit - output.Length;
            if (remaining <= 0)
            {
                break;
            }

            output.Append(chunk.AsSpan(0, Math.Min(chunk.Length, remaining)));
        }

        var sanitized = output.ToString()
            .Replace(secret, "[REDACTED]", StringComparison.Ordinal)
            .Replace(request.BaselineDumpPath, "<baseline-dump>", StringComparison.OrdinalIgnoreCase);
        if (sanitized.Length > request.OutputLimit)
        {
            sanitized = sanitized[..request.OutputLimit];
        }

        return string.IsNullOrWhiteSpace(sanitized)
            ? "The PostgreSQL tool failed without diagnostic output."
            : sanitized;
    }

    private static int CredentialAwareCaptureLimit(PostgreSqlRestoreRequest request) =>
        checked(request.OutputLimit + request.Target.Credential.Reveal().Length);

    private sealed record StepResult(PostgreSqlToolResult? Result, PostgreSqlRestoreFailure? Failure);
}
