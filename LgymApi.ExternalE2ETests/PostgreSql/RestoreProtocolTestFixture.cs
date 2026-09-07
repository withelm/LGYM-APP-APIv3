using System.Collections.Immutable;

namespace LgymApi.ExternalE2ETests.PostgreSql;

internal sealed class RestoreProtocolTestFixture : IDisposable
{
    internal const string ExpectedHost = "localhost";
    internal const string ExpectedDatabase = "lgym_external_e2e";
    internal const string ExpectedMarker = "lgym_external_e2e_v1";
    internal const string Password = "split-secret-canary";

    private readonly string _directory;

    public RestoreProtocolTestFixture()
    {
        _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_directory);
        DumpPath = Path.Combine(_directory, "baseline.bak");
        File.WriteAllText(DumpPath, "fake archive read only through the injected runner");
    }

    public string DumpPath { get; }

    public PostgreSqlRestoreRequest CreateRequest() => new(
        new PostgreSqlTarget(
            ExpectedHost,
            5432,
            ExpectedDatabase,
            "lgym_external_e2e_runner",
            new PostgreSqlCredential(Password)),
        new PostgreSqlExpectedIdentity(ExpectedHost, ExpectedDatabase, ExpectedMarker),
        DumpPath,
        TimeSpan.FromSeconds(30),
        4096);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    public static PostgreSqlToolResult Success(params string[] chunks) =>
        new(0, TimedOut: false, chunks.ToImmutableArray());

    public static PostgreSqlToolResult Failure(int exitCode, params string[] chunks) =>
        new(exitCode, TimedOut: false, chunks.ToImmutableArray());
}

internal sealed class FakePostgreSqlToolRunner : IPostgreSqlToolRunner
{
    private readonly Queue<Func<PostgreSqlToolResult>> _responses = new();
    private readonly List<PostgreSqlToolCommand> _commands = new();
    private readonly List<bool> _credentialSupplied = new();

    public IReadOnlyList<PostgreSqlToolCommand> Commands => _commands.AsReadOnly();

    public IReadOnlyList<bool> CredentialSupplied => _credentialSupplied.AsReadOnly();

    public void Enqueue(PostgreSqlToolResult result) => _responses.Enqueue(() => result);

    public void EnqueueException(Exception exception) => _responses.Enqueue(() => throw exception);

    public ValueTask<PostgreSqlToolResult> RunAsync(
        PostgreSqlToolCommand command,
        PostgreSqlCredential? credential,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _commands.Add(command);
        _credentialSupplied.Add(credential is not null);

        if (!_responses.TryDequeue(out var response))
        {
            throw new InvalidOperationException("No fake PostgreSQL tool response was configured.");
        }

        return ValueTask.FromResult(response());
    }
}
