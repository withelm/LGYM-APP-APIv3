using NUnit.Framework;

namespace LgymApi.ExternalE2ETests.PostgreSql;

[TestFixture]
[Category("PostgreSqlRestoreProtocol")]
public sealed class RestoreProtocolSafetyTests
{
    [TestCase(UnsafeRequest.HostMismatch)]
    [TestCase(UnsafeRequest.DatabaseMismatch)]
    [TestCase(UnsafeRequest.UnsafeDatabaseName)]
    [TestCase(UnsafeRequest.UnsafeMarker)]
    [TestCase(UnsafeRequest.RelativeDumpPath)]
    [TestCase(UnsafeRequest.WrongDumpExtension)]
    [TestCase(UnsafeRequest.InvalidPort)]
    [TestCase(UnsafeRequest.LegacyDatabaseName)]
    [TestCase(UnsafeRequest.LegacyMarker)]
    public void Test_restore_when_request_is_unsafe_rejects_before_any_tool_invocation(UnsafeRequest unsafeRequest)
    {
        using var fixture = new RestoreProtocolTestFixture();
        var runner = new FakePostgreSqlToolRunner();
        var protocol = new PostgreSqlRestoreProtocol(runner);
        var request = MakeUnsafe(fixture.CreateRequest(), unsafeRequest);

        var exception = Assert.ThrowsAsync<PostgreSqlRestoreValidationException>(
            async () => await protocol.RestoreAsync(request));

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.Message, Does.Contain("rejected before PostgreSQL tools were invoked"));
            Assert.That(exception.ToString(), Does.Not.Contain(RestoreProtocolTestFixture.Password));
            Assert.That(runner.Commands, Is.Empty);
        });
    }

    [Test]
    public void Test_restore_when_dump_does_not_exist_rejects_before_any_tool_invocation()
    {
        using var fixture = new RestoreProtocolTestFixture();
        var runner = new FakePostgreSqlToolRunner();
        var protocol = new PostgreSqlRestoreProtocol(runner);
        var request = fixture.CreateRequest() with { BaselineDumpPath = Path.Combine(Path.GetTempPath(), "missing.bak") };

        Assert.ThrowsAsync<PostgreSqlRestoreValidationException>(async () => await protocol.RestoreAsync(request));

        Assert.That(runner.Commands, Is.Empty);
    }

    [TestCase(1, "not a PostgreSQL archive")]
    [TestCase(0, "misleading success without a table-of-contents entry")]
    public async Task Test_restore_when_dump_is_not_listable_fails_before_marker_or_termination(int exitCode, string output)
    {
        using var fixture = new RestoreProtocolTestFixture();
        var runner = new FakePostgreSqlToolRunner();
        runner.Enqueue(RestoreProtocolTestFixture.Success("psql 18"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("pg_restore 18"));
        runner.Enqueue(new PostgreSqlToolResult(exitCode, TimedOut: false, [output]));
        var protocol = new PostgreSqlRestoreProtocol(runner);

        var result = await protocol.RestoreAsync(fixture.CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.IsHealthy, Is.False);
            Assert.That(result.Failure?.Stage, Is.EqualTo(PostgreSqlRestoreStage.ListDump));
            Assert.That(runner.Commands, Has.Count.EqualTo(3));
            Assert.That(runner.Commands, Has.None.Matches<PostgreSqlToolCommand>(
                command => command.Stage is PostgreSqlRestoreStage.PreRestoreMarker or PostgreSqlRestoreStage.QuiesceTarget));
        });
    }

    [TestCase("lgym_external_e2e", 101, 102, true)]
    [TestCase("other_application_database", 101, 102, false)]
    [TestCase("lgym_external_e2e", 102, 102, false)]
    public void Test_session_fixture_when_scope_is_evaluated_terminates_only_other_target_sessions(
        string sessionDatabase,
        int sessionProcessId,
        int currentProcessId,
        bool expected)
    {
        var session = new PostgreSqlSessionFixture(sessionDatabase, sessionProcessId);

        var shouldTerminate = PostgreSqlTargetSessionScope.ShouldTerminate(
            session,
            RestoreProtocolTestFixture.ExpectedDatabase,
            currentProcessId);

        Assert.That(shouldTerminate, Is.EqualTo(expected));
    }

    [Test]
    public async Task Test_quiescence_command_when_constructed_has_exact_target_and_peer_predicates()
    {
        using var fixture = new RestoreProtocolTestFixture();
        var runner = new FakePostgreSqlToolRunner();
        runner.Enqueue(RestoreProtocolTestFixture.Success("psql 18"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("pg_restore 18"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("1; archive entry"));
        runner.Enqueue(RestoreProtocolTestFixture.Success(RestoreProtocolTestFixture.ExpectedMarker));
        runner.Enqueue(RestoreProtocolTestFixture.Success("t"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("restore complete"));
        runner.Enqueue(RestoreProtocolTestFixture.Success(RestoreProtocolTestFixture.ExpectedMarker));
        var protocol = new PostgreSqlRestoreProtocol(runner);

        var result = await protocol.RestoreAsync(fixture.CreateRequest());
        var command = runner.Commands.Single(item => item.Stage == PostgreSqlRestoreStage.QuiesceTarget);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsHealthy, Is.True);
            Assert.That(command.StandardInput, Does.Contain("datname = current_database()"));
            Assert.That(command.StandardInput, Does.Contain("datname = :'expected_database'"));
            Assert.That(command.StandardInput, Does.Contain("pid <> pg_backend_pid()"));
            Assert.That(command.Arguments, Does.Contain($"expected_database={RestoreProtocolTestFixture.ExpectedDatabase}"));
            Assert.That(command.StandardInput, Does.Not.Contain("other_application_database"));
            Assert.That(command.SafeReceipt, Is.EqualTo("quiesce-target: psql <validated-target> <fixed-query>"));
        });
    }

    [Test]
    public async Task Test_post_restore_marker_when_missing_reports_unhealthy_target_without_cleanup()
    {
        using var fixture = new RestoreProtocolTestFixture();
        var runner = new FakePostgreSqlToolRunner();
        runner.Enqueue(RestoreProtocolTestFixture.Success("psql 18"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("pg_restore 18"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("1; archive entry"));
        runner.Enqueue(RestoreProtocolTestFixture.Success(RestoreProtocolTestFixture.ExpectedMarker));
        runner.Enqueue(RestoreProtocolTestFixture.Success("t"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("restore complete"));
        runner.Enqueue(RestoreProtocolTestFixture.Success("stale-marker"));
        var protocol = new PostgreSqlRestoreProtocol(runner);

        var result = await protocol.RestoreAsync(fixture.CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.IsHealthy, Is.False);
            Assert.That(result.Failure?.Stage, Is.EqualTo(PostgreSqlRestoreStage.PostRestoreMarker));
            Assert.That(runner.Commands, Has.Count.EqualTo(7));
            Assert.That(runner.Commands.Count(command => command.Stage == PostgreSqlRestoreStage.QuiesceTarget), Is.EqualTo(1));
            Assert.That(runner.Commands.Count(command => command.Stage == PostgreSqlRestoreStage.Restore), Is.EqualTo(1));
        });
    }

    private static PostgreSqlRestoreRequest MakeUnsafe(PostgreSqlRestoreRequest request, UnsafeRequest unsafeRequest) =>
        unsafeRequest switch
        {
            UnsafeRequest.HostMismatch => request with
            {
                ExpectedIdentity = request.ExpectedIdentity with { Host = "different.e2e.local" }
            },
            UnsafeRequest.DatabaseMismatch => request with
            {
                ExpectedIdentity = request.ExpectedIdentity with { DatabaseName = "lgym_external_e2e_different" }
            },
            UnsafeRequest.UnsafeDatabaseName => request with
            {
                Target = request.Target with { DatabaseName = "postgres; DROP DATABASE postgres" },
                ExpectedIdentity = request.ExpectedIdentity with { DatabaseName = "postgres; DROP DATABASE postgres" }
            },
            UnsafeRequest.UnsafeMarker => request with
            {
                ExpectedIdentity = request.ExpectedIdentity with { Marker = "baseline'; SELECT true; --" }
            },
            UnsafeRequest.RelativeDumpPath => request with { BaselineDumpPath = "baseline.bak" },
            UnsafeRequest.WrongDumpExtension => request with
            {
                BaselineDumpPath = Path.ChangeExtension(request.BaselineDumpPath, ".sql")
            },
            UnsafeRequest.InvalidPort => request with { Target = request.Target with { Port = 0 } },
            UnsafeRequest.LegacyDatabaseName => request with
            {
                Target = request.Target with { DatabaseName = "lgym_e2e_external" },
                ExpectedIdentity = request.ExpectedIdentity with { DatabaseName = "lgym_e2e_external" }
            },
            UnsafeRequest.LegacyMarker => request with
            {
                ExpectedIdentity = request.ExpectedIdentity with { Marker = "lgym-external-e2e:baseline-v1" }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(unsafeRequest), unsafeRequest, null)
        };

    public enum UnsafeRequest
    {
        HostMismatch,
        DatabaseMismatch,
        UnsafeDatabaseName,
        UnsafeMarker,
        RelativeDumpPath,
        WrongDumpExtension,
        InvalidPort,
        LegacyDatabaseName,
        LegacyMarker
    }
}
