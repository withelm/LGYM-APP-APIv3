using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;

namespace LgymApi.ExternalE2ETests.PostgreSql;

internal enum PostgreSqlTool
{
    Psql,
    PgRestore
}

internal enum PostgreSqlRestoreStage
{
    DiscoverPsql,
    DiscoverPgRestore,
    ListDump,
    PreRestoreMarker,
    QuiesceTarget,
    Restore,
    PostRestoreMarker
}

internal sealed class PostgreSqlCredential
{
    private readonly string _value;

    public PostgreSqlCredential(string value)
    {
        _value = value;
    }

    internal string Reveal() => _value;

    public override string ToString() => "[REDACTED]";
}

internal sealed record PostgreSqlToolCommand(
    PostgreSqlTool Tool,
    PostgreSqlRestoreStage Stage,
    ImmutableArray<string> Arguments,
    string? StandardInput,
    TimeSpan Timeout,
    int OutputLimit,
    string SafeReceipt);

internal sealed record PostgreSqlToolResult(
    int ExitCode,
    bool TimedOut,
    ImmutableArray<string> OutputChunks);

internal interface IPostgreSqlToolRunner
{
    ValueTask<PostgreSqlToolResult> RunAsync(
        PostgreSqlToolCommand command,
        PostgreSqlCredential? credential,
        CancellationToken cancellationToken);
}

internal sealed class SystemPostgreSqlToolRunner : IPostgreSqlToolRunner
{
    public async ValueTask<PostgreSqlToolResult> RunAsync(
        PostgreSqlToolCommand command,
        PostgreSqlCredential? credential,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = CreateStartInfo(command, credential) };
        process.Start();

        var chunks = new ConcurrentQueue<string>();
        var remainingOutput = new OutputBudget(command.OutputLimit);
        var standardOutput = CaptureAsync(process.StandardOutput, chunks, remainingOutput);
        var standardError = CaptureAsync(process.StandardError, chunks, remainingOutput);

        if (command.StandardInput is not null)
        {
            await process.StandardInput.WriteAsync(command.StandardInput.AsMemory(), cancellationToken);
        }

        process.StandardInput.Close();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(command.Timeout);
        var timedOut = false;

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }

        await Task.WhenAll(standardOutput, standardError);
        return new PostgreSqlToolResult(process.ExitCode, timedOut, chunks.ToImmutableArray());
    }

    private static ProcessStartInfo CreateStartInfo(
        PostgreSqlToolCommand command,
        PostgreSqlCredential? credential)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command.Tool == PostgreSqlTool.Psql ? "psql" : "pg_restore",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (credential is not null)
        {
            startInfo.Environment["PGPASSWORD"] = credential.Reveal();
        }

        return startInfo;
    }

    private static async Task CaptureAsync(
        StreamReader reader,
        ConcurrentQueue<string> chunks,
        OutputBudget budget)
    {
        var buffer = new char[1024];
        int read;
        while ((read = await reader.ReadAsync(buffer)) > 0)
        {
            var accepted = budget.Take(read);
            if (accepted > 0)
            {
                chunks.Enqueue(new string(buffer, 0, accepted));
            }
        }
    }

    private sealed class OutputBudget
    {
        private int _remaining;

        public OutputBudget(int limit)
        {
            _remaining = limit;
        }

        public int Take(int requested)
        {
            while (true)
            {
                var remaining = Volatile.Read(ref _remaining);
                if (remaining <= 0)
                {
                    return 0;
                }

                var accepted = Math.Min(requested, remaining);
                if (Interlocked.CompareExchange(ref _remaining, remaining - accepted, remaining) == remaining)
                {
                    return accepted;
                }
            }
        }
    }
}
