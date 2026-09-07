using NUnit.Framework;

namespace LgymApi.ExternalE2ETests.PostgreSql;

[TestFixture]
[Category("PostgreSqlRestoreProtocol")]
public sealed class RestoreProtocolTests
{
    [Test]
    public async Task Test_restore_when_todo_2_identity_contract_is_used_accepts_the_fixed_safe_command_order()
    {
        using var fixture = new RestoreProtocolTestFixture();
        var runner = CreateSuccessfulRunner();
        var protocol = new PostgreSqlRestoreProtocol(runner);

        var result = await protocol.RestoreAsync(fixture.CreateRequest());

        TestContext.Out.WriteLine(string.Join(Environment.NewLine, result.CommandReceipts));
        TestContext.Out.WriteLine("session-scope: target-peer=true; foreign-peer=false; current-session=false");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsHealthy, Is.True);
            Assert.That(result.Failure, Is.Null);
            Assert.That(
                runner.Commands.Select(command => command.Stage),
                Is.EqualTo(new[]
                {
                    PostgreSqlRestoreStage.DiscoverPsql,
                    PostgreSqlRestoreStage.DiscoverPgRestore,
                    PostgreSqlRestoreStage.ListDump,
                    PostgreSqlRestoreStage.PreRestoreMarker,
                    PostgreSqlRestoreStage.QuiesceTarget,
                    PostgreSqlRestoreStage.Restore,
                    PostgreSqlRestoreStage.PostRestoreMarker
                }));
            Assert.That(result.CommandReceipts, Is.EqualTo(runner.Commands.Select(command => command.SafeReceipt)));
            Assert.That(runner.CredentialSupplied, Is.EqualTo(new[] { false, false, false, true, true, true, true }));
        });
    }

    [Test]
    public async Task Test_restore_command_when_constructed_uses_required_flags_without_secrets_or_shell_text()
    {
        using var fixture = new RestoreProtocolTestFixture();
        var runner = CreateSuccessfulRunner();
        var protocol = new PostgreSqlRestoreProtocol(runner);

        var result = await protocol.RestoreAsync(fixture.CreateRequest());
        var command = runner.Commands.Single(item => item.Stage == PostgreSqlRestoreStage.Restore);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsHealthy, Is.True);
            Assert.That(command.Tool, Is.EqualTo(PostgreSqlTool.PgRestore));
            Assert.That(command.StandardInput, Is.Null);
            Assert.That(command.Arguments, Does.Contain("--clean"));
            Assert.That(command.Arguments, Does.Contain("--if-exists"));
            Assert.That(command.Arguments, Does.Contain("--no-owner"));
            Assert.That(command.Arguments, Does.Contain("--no-privileges"));
            Assert.That(command.Arguments, Does.Contain("--exit-on-error"));
            Assert.That(command.Arguments, Does.Contain("--dbname"));
            Assert.That(command.Arguments, Does.Contain(RestoreProtocolTestFixture.ExpectedDatabase));
            Assert.That(command.Arguments, Does.Not.Contain(RestoreProtocolTestFixture.Password));
            Assert.That(command.SafeReceipt, Does.Not.Contain(fixture.DumpPath));
            Assert.That(command.SafeReceipt, Does.Not.Contain(RestoreProtocolTestFixture.Password));
            Assert.That(command.SafeReceipt, Does.Not.Contain("Host="));
        });
    }

    [Test]
    public async Task Test_restore_when_pre_restore_marker_is_missing_never_quiesces_or_restores()
    {
        using var fixture = new RestoreProtocolTestFixture();
        var runner = new FakePostgreSqlToolRunner();
        runner.Enqueue(RestoreProtocolTestFixture.Success("psql 18"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("pg_restore 18"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("1; archive entry"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("unexpected-marker"));
        var protocol = new PostgreSqlRestoreProtocol(runner);

        var result = await protocol.RestoreAsync(fixture.CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.IsHealthy, Is.False);
            Assert.That(result.Failure?.Stage, Is.EqualTo(PostgreSqlRestoreStage.PreRestoreMarker));
            Assert.That(runner.Commands, Has.Count.EqualTo(4));
            Assert.That(runner.Commands, Has.None.Matches<PostgreSqlToolCommand>(
                command => command.Stage is PostgreSqlRestoreStage.QuiesceTarget or PostgreSqlRestoreStage.Restore));
        });
    }

    [Test]
    public async Task Test_restore_when_quiescence_reports_a_failed_termination_never_runs_pg_restore()
    {
        using var fixture = new RestoreProtocolTestFixture();
        var runner = new FakePostgreSqlToolRunner();
        runner.Enqueue(RestoreProtocolTestFixture.Success("psql 18"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("pg_restore 18"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("1; archive entry"));
        runner.Enqueue(RestoreProtocolTestFixture.Success(RestoreProtocolTestFixture.ExpectedMarker));
        runner.Enqueue(RestoreProtocolTestFixture.Success("f"));
        var protocol = new PostgreSqlRestoreProtocol(runner);

        var result = await protocol.RestoreAsync(fixture.CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.IsHealthy, Is.False);
            Assert.That(result.Failure?.Stage, Is.EqualTo(PostgreSqlRestoreStage.QuiesceTarget));
            Assert.That(runner.Commands, Has.None.Matches<PostgreSqlToolCommand>(
                command => command.Stage == PostgreSqlRestoreStage.Restore));
        });
    }

    [Test]
    public async Task Test_restore_when_pg_restore_fails_preserves_primary_stage_and_redacts_split_chunks()
    {
        using var fixture = new RestoreProtocolTestFixture();
        var runner = new FakePostgreSqlToolRunner();
        runner.Enqueue(RestoreProtocolTestFixture.Success("psql 18"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("pg_restore 18"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("1; archive entry"));
        runner.Enqueue(RestoreProtocolTestFixture.Success(RestoreProtocolTestFixture.ExpectedMarker));
        runner.Enqueue(RestoreProtocolTestFixture.Success("t"));
        runner.Enqueue(RestoreProtocolTestFixture.Failure(1, "failure echoed split-", "secret-canary then stopped"));
        var protocol = new PostgreSqlRestoreProtocol(runner);

        var result = await protocol.RestoreAsync(fixture.CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.IsHealthy, Is.False);
            Assert.That(result.Failure?.Stage, Is.EqualTo(PostgreSqlRestoreStage.Restore));
            Assert.That(result.Failure?.ExitCode, Is.EqualTo(1));
            Assert.That(result.Failure?.SanitizedDiagnostic, Does.Contain("[REDACTED]"));
            Assert.That(result.ToString(), Does.Not.Contain(RestoreProtocolTestFixture.Password));
            Assert.That(runner.Commands, Has.Count.EqualTo(6));
            Assert.That(runner.Commands, Has.None.Matches<PostgreSqlToolCommand>(
                command => command.Stage == PostgreSqlRestoreStage.PostRestoreMarker));
        });
    }

    [Test]
    public async Task Test_restore_when_runner_throws_during_restore_keeps_restore_as_primary_failure()
    {
        using var fixture = new RestoreProtocolTestFixture();
        var runner = new FakePostgreSqlToolRunner();
        runner.Enqueue(RestoreProtocolTestFixture.Success("psql 18"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("pg_restore 18"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("1; archive entry"));
        runner.Enqueue(RestoreProtocolTestFixture.Success(RestoreProtocolTestFixture.ExpectedMarker));
        runner.Enqueue(RestoreProtocolTestFixture.Success("t"));
        runner.EnqueueException(new IOException($"interrupted {RestoreProtocolTestFixture.Password}"));
        var protocol = new PostgreSqlRestoreProtocol(runner);

        var result = await protocol.RestoreAsync(fixture.CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.IsHealthy, Is.False);
            Assert.That(result.Failure?.Stage, Is.EqualTo(PostgreSqlRestoreStage.Restore));
            Assert.That(result.Failure?.SanitizedDiagnostic, Does.Not.Contain(RestoreProtocolTestFixture.Password));
            Assert.That(result.Failure?.SanitizedDiagnostic, Does.Contain("[REDACTED]"));
            Assert.That(runner.Commands, Has.Count.EqualTo(6));
        });
    }

    [Test]
    public async Task Test_restore_when_a_prior_run_was_interrupted_does_not_reuse_stale_failure_state()
    {
        using var fixture = new RestoreProtocolTestFixture();
        var runner = new FakePostgreSqlToolRunner();
        runner.Enqueue(RestoreProtocolTestFixture.Success("psql 18"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("pg_restore 18"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("1; archive entry"));
        runner.Enqueue(RestoreProtocolTestFixture.Success(RestoreProtocolTestFixture.ExpectedMarker));
        runner.Enqueue(RestoreProtocolTestFixture.Success("t"));
        runner.EnqueueException(new IOException("first restore interrupted"));
        EnqueueSuccessfulRun(runner);
        var protocol = new PostgreSqlRestoreProtocol(runner);

        var first = await protocol.RestoreAsync(fixture.CreateRequest());
        var second = await protocol.RestoreAsync(fixture.CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(first.Failure?.Stage, Is.EqualTo(PostgreSqlRestoreStage.Restore));
            Assert.That(second.IsHealthy, Is.True);
            Assert.That(second.Failure, Is.Null);
            Assert.That(second.CommandReceipts.Length, Is.EqualTo(7));
            Assert.That(runner.Commands, Has.Count.EqualTo(13));
        });
    }

    private static FakePostgreSqlToolRunner CreateSuccessfulRunner()
    {
        var runner = new FakePostgreSqlToolRunner();
        EnqueueSuccessfulRun(runner);
        return runner;
    }

    private static void EnqueueSuccessfulRun(FakePostgreSqlToolRunner runner)
    {
        runner.Enqueue(RestoreProtocolTestFixture.Success("psql 18"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("pg_restore 18"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("1; archive entry"));
        runner.Enqueue(RestoreProtocolTestFixture.Success(RestoreProtocolTestFixture.ExpectedMarker));
        runner.Enqueue(RestoreProtocolTestFixture.Success("t"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("restore complete"));
        runner.Enqueue(RestoreProtocolTestFixture.Success(RestoreProtocolTestFixture.ExpectedMarker));
    }
}
