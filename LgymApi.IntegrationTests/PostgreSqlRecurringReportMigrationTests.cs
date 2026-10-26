using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("PostgreSql")]
public sealed class PostgreSqlRecurringReportMigrationTests
{
    private const string PreRepairMigrationId = "20260710201009_AddPushNotificationMessages";
    private const string OrphanRepairMigrationId = "20260710120000_FixRecurringReportAssignmentIdIndex";
    private const string RepairMigrationName = "RepairRecurringReportAssignmentRequestIndex";
    private const string RecurringAssignmentIndexName = "IX_ReportRequests_RecurringReportAssignmentId";
    private const string PreviousMigrationId = "20260815080018_AddNotificationRetentionIndexes";
    private const string ReceiptMigrationId = "20261026222317_AddReportSubmissionProgressReceipts";
    private const string ReceiptTableName = "ReportSubmissionProgressReceipts";
    private const string ReceiptIndexName = "IX_ReportSubmissionProgressReceipts_ReportSubmissionId";

    [Test]
    public async Task PreRepairTarget_ExcludesOrphanFromDiscoveryAndHistory_AndRetainsUniqueIndex()
    {
        await using var lease = await PostgreSqlDatabaseLease.CreateAsync();

        await using (var migrationContext = CreateDbContext(lease))
        {
            var migrations = migrationContext.Database.GetMigrations().ToList();

            migrations.Should().Contain(PreRepairMigrationId);
            migrations.Should().NotContain(OrphanRepairMigrationId);

            await MigrateAsync(migrationContext, PreRepairMigrationId);
        }

        await using var verificationContext = CreateDbContext(lease);
        var history = (await verificationContext.Database.GetAppliedMigrationsAsync()).ToList();

        history.Should().Contain(PreRepairMigrationId);
        history.Should().NotContain(OrphanRepairMigrationId);
        var indexIsUnique = await GetIndexIsUniqueAsync(verificationContext);
        indexIsUnique.Should().BeTrue();
        TestContext.Progress.WriteLine(
            $"Baseline target: index unique={indexIsUnique}; history={string.Join(',', history.TakeLast(1))}");
    }

    [Test]
    public async Task ForwardMigration_FromUniqueIndex_RecreatesItAsNonUniqueAndAllowsMultipleRequests()
    {
        await using var lease = await PostgreSqlDatabaseLease.CreateAsync();
        RecurringRequestSeed seed;

        await using (var baselineContext = CreateDbContext(lease))
        {
            await MigrateAsync(baselineContext, PreRepairMigrationId);
            seed = await SeedFirstRecurringRequestAsync(baselineContext);
        }

        await using (var duplicateContext = CreateDbContext(lease))
        {
            await AssertDuplicateRequestIsRejectedAsync(duplicateContext, seed);
        }

        await using (var forwardContext = CreateDbContext(lease))
        {
            await forwardContext.Database.MigrateAsync();
        }

        await using (var schemaContext = CreateDbContext(lease))
        {
            var indexIsUnique = await GetIndexIsUniqueAsync(schemaContext);
            indexIsUnique.Should().BeFalse();
            schemaContext.Database.HasPendingModelChanges().Should().BeFalse();

            var migrations = schemaContext.Database.GetMigrations().ToList();
            var repairMigrationId = GetRepairMigrationId(migrations);
            migrations.IndexOf(PreRepairMigrationId).Should().Be(migrations.IndexOf(repairMigrationId) - 1);

            var history = (await schemaContext.Database.GetAppliedMigrationsAsync()).ToList();
            history.Should().Contain(repairMigrationId);
            TestContext.Progress.WriteLine(
                $"Forward repair from unique: index unique={indexIsUnique}; history={string.Join(',', history.TakeLast(2))}");
        }

        await using (var secondRequestContext = CreateDbContext(lease))
        {
            secondRequestContext.ReportRequests.Add(CreateRequest(seed, "second"));
            await secondRequestContext.SaveChangesAsync();
        }

        await using var verificationContext = CreateDbContext(lease);
        (await verificationContext.ReportRequests
                .AsNoTracking()
                .CountAsync(request => request.RecurringReportAssignmentId == seed.AssignmentId))
            .Should()
            .Be(2);
    }

    [Test]
    public async Task ForwardMigration_WhenIndexIsAlreadyNonUnique_PreservesRequestsAndRepairHistory()
    {
        await using var lease = await PostgreSqlDatabaseLease.CreateAsync();
        RecurringRequestSeed seed;

        await using (var baselineContext = CreateDbContext(lease))
        {
            await MigrateAsync(baselineContext, PreRepairMigrationId);
            await RecreateIndexAsNonUniqueAsync(baselineContext);
            seed = await SeedFirstRecurringRequestAsync(baselineContext);
        }

        await using (var forwardContext = CreateDbContext(lease))
        {
            await forwardContext.Database.MigrateAsync();
        }

        await using (var verificationContext = CreateDbContext(lease))
        {
            var indexIsUnique = await GetIndexIsUniqueAsync(verificationContext);
            indexIsUnique.Should().BeFalse();
            var repairMigrationId = GetRepairMigrationId(verificationContext.Database.GetMigrations());
            var history = (await verificationContext.Database.GetAppliedMigrationsAsync()).ToList();
            history.Should().Contain(repairMigrationId);
            (await verificationContext.ReportRequests
                    .AsNoTracking()
                    .CountAsync(request => request.RecurringReportAssignmentId == seed.AssignmentId))
                .Should()
                .Be(1);
            TestContext.Progress.WriteLine(
                $"Forward repair from non-unique: index unique={indexIsUnique}; history={string.Join(',', history.TakeLast(2))}");
        }

        await using (var secondRequestContext = CreateDbContext(lease))
        {
            secondRequestContext.ReportRequests.Add(CreateRequest(seed, "second"));
            await secondRequestContext.SaveChangesAsync();
        }

        await using var countContext = CreateDbContext(lease);
        (await countContext.ReportRequests
                .AsNoTracking()
                .CountAsync(request => request.RecurringReportAssignmentId == seed.AssignmentId))
            .Should()
            .Be(2);
    }

    [Test]
    public async Task ForwardMigration_DownIsRejectedAfterMultipleRequestsAndLeavesDatabaseUnchanged()
    {
        await using var lease = await PostgreSqlDatabaseLease.CreateAsync();
        RecurringRequestSeed seed;
        string repairMigrationId;

        await using (var baselineContext = CreateDbContext(lease))
        {
            await MigrateAsync(baselineContext, PreRepairMigrationId);
        }

        await using (var forwardContext = CreateDbContext(lease))
        {
            await forwardContext.Database.MigrateAsync();
            repairMigrationId = GetRepairMigrationId(forwardContext.Database.GetMigrations());
        }

        await using (var writeContext = CreateDbContext(lease))
        {
            seed = await SeedFirstRecurringRequestAsync(writeContext);
            writeContext.ReportRequests.Add(CreateRequest(seed, "second"));
            await writeContext.SaveChangesAsync();
        }

        await using (var downgradeContext = CreateDbContext(lease))
        {
            Func<Task> downgrade = () => MigrateAsync(downgradeContext, PreRepairMigrationId);

            await downgrade.Should().ThrowAsync<NotSupportedException>()
                .WithMessage("*restoring uniqueness is unsafe after multiple history rows*");
        }

        await using var verificationContext = CreateDbContext(lease);
        var indexIsUnique = await GetIndexIsUniqueAsync(verificationContext);
        indexIsUnique.Should().BeFalse();
        var history = (await verificationContext.Database.GetAppliedMigrationsAsync()).ToList();
        history.Should().Contain(repairMigrationId);
        var requestCount = await verificationContext.ReportRequests
                .AsNoTracking()
                .CountAsync(request => request.RecurringReportAssignmentId == seed.AssignmentId);
        requestCount.Should().Be(2);
        TestContext.Progress.WriteLine(
            $"Rejected downgrade: index unique={indexIsUnique}; requests={requestCount}; history={string.Join(',', history.TakeLast(2))}");
    }

    [Test]
    public async Task ReceiptMigration_FromPreviousMigration_PreservesReportAndMeasurementDataAndCreatesUniqueReceiptIndex()
    {
        await using var lease = await PostgreSqlDatabaseLease.CreateAsync();
        MigrationPreservationSeed seed;

        await using (var baselineContext = CreateDbContext(lease))
        {
            var migrations = baselineContext.Database.GetMigrations().ToList();
            migrations.IndexOf(PreviousMigrationId).Should().Be(migrations.IndexOf(ReceiptMigrationId) - 1);
            await MigrateAsync(baselineContext, PreviousMigrationId);
            seed = await SeedMigrationPreservationDataAsync(baselineContext);
        }

        await using (var migrationContext = CreateDbContext(lease))
        {
            await MigrateAsync(migrationContext, ReceiptMigrationId);
        }

        await using var verificationContext = CreateDbContext(lease);
        var submission = await verificationContext.ReportSubmissions
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == seed.SubmissionId);
        var measurement = await verificationContext.Measurements
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == seed.MeasurementId);
        var appliedMigrations = (await verificationContext.Database.GetAppliedMigrationsAsync()).ToList();

        Assert.Multiple(() =>
        {
            submission.ReportRequestId.Should().Be(seed.RequestId);
            submission.TraineeId.Should().Be(seed.TraineeId);
            submission.PayloadJson.Should().Be(seed.PayloadJson);
            measurement.UserId.Should().Be(seed.TraineeId);
            measurement.BodyPart.Should().Be(seed.BodyPart);
            measurement.Unit.Should().Be(seed.Unit);
            measurement.Value.Should().Be(seed.Value);
            measurement.CreatedAt.Should().Be(seed.CreatedAtUtc);
            appliedMigrations.Should().Contain(ReceiptMigrationId);
        });
        (await TableExistsAsync(verificationContext, ReceiptTableName)).Should().BeTrue();
        (await GetIndexIsUniqueAsync(verificationContext, ReceiptIndexName)).Should().BeTrue();
        TestContext.Progress.WriteLine(
            $"Receipt migration preservation: submission={submission.Id}; measurement={measurement.Id}; table={ReceiptTableName}; uniqueIndex={ReceiptIndexName}");
    }

    private static AppDbContext CreateDbContext(PostgreSqlDatabaseLease lease)
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(lease.ConnectionString)
            .Options);

    private static Task MigrateAsync(AppDbContext dbContext, string targetMigration)
        => dbContext.Database.GetService<IMigrator>().MigrateAsync(targetMigration);

    private static string GetRepairMigrationId(IEnumerable<string> migrations)
    {
        var matchingMigrations = migrations
            .Where(migration => migration.EndsWith(RepairMigrationName, StringComparison.Ordinal))
            .ToList();

        matchingMigrations.Should().ContainSingle();
        return matchingMigrations[0];
    }

    private static async Task<bool> GetIndexIsUniqueAsync(
        AppDbContext dbContext,
        string indexName = RecurringAssignmentIndexName)
    {
        await dbContext.Database.OpenConnectionAsync();
        try
        {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT index_info.indisunique
                FROM pg_index AS index_info
                INNER JOIN pg_class AS index_class ON index_class.oid = index_info.indexrelid
                WHERE index_class.relname = @indexName
                """;

            var indexNameParameter = command.CreateParameter();
            indexNameParameter.ParameterName = "indexName";
            indexNameParameter.Value = indexName;
            command.Parameters.Add(indexNameParameter);

            return (bool)(await command.ExecuteScalarAsync())!;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static async Task<bool> TableExistsAsync(AppDbContext dbContext, string tableName)
    {
        await dbContext.Database.OpenConnectionAsync();
        try
        {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT to_regclass('public.' || quote_ident(@tableName)) IS NOT NULL";
            var tableNameParameter = command.CreateParameter();
            tableNameParameter.ParameterName = "tableName";
            tableNameParameter.Value = tableName;
            command.Parameters.Add(tableNameParameter);
            return (bool)(await command.ExecuteScalarAsync())!;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static async Task RecreateIndexAsNonUniqueAsync(AppDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            "DROP INDEX IF EXISTS \"IX_ReportRequests_RecurringReportAssignmentId\"; " +
            "CREATE INDEX \"IX_ReportRequests_RecurringReportAssignmentId\" ON \"ReportRequests\" (\"RecurringReportAssignmentId\");");
    }

    private static async Task<RecurringRequestSeed> SeedFirstRecurringRequestAsync(AppDbContext dbContext)
    {
        var trainer = CreateUser("trainer");
        var trainee = CreateUser("trainee");
        var template = new ReportTemplate
        {
            Id = Id<ReportTemplate>.New(),
            TrainerId = trainer.Id,
            Name = "Recurring migration template"
        };
        var assignment = new RecurringReportAssignment
        {
            Id = Id<RecurringReportAssignment>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            TemplateId = template.Id,
            IntervalValue = 1,
            IntervalUnit = RecurringReportIntervalUnit.Week,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-7),
            IsActive = true
        };
        var seed = new RecurringRequestSeed(trainer.Id, trainee.Id, template.Id, assignment.Id);

        dbContext.AddRange(trainer, trainee, template, assignment, CreateRequest(seed, "first"));
        await dbContext.SaveChangesAsync();

        return seed;
    }

    private static async Task<MigrationPreservationSeed> SeedMigrationPreservationDataAsync(AppDbContext dbContext)
    {
        var trainer = CreateUser("receipt-migration-trainer");
        var trainee = CreateUser("receipt-migration-trainee");
        var template = new ReportTemplate
        {
            Id = Id<ReportTemplate>.New(),
            TrainerId = trainer.Id,
            Name = "Receipt migration template"
        };
        var request = new ReportRequest
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = trainer.Id,
            TraineeId = trainee.Id,
            TemplateId = template.Id,
            Status = ReportRequestStatus.Submitted
        };
        const string payloadJson = "{\"measurements\":{\"Waist\":{\"value\":87.125,\"unit\":\"Centimeters\"}}}";
        var submission = new ReportSubmission
        {
            Id = Id<ReportSubmission>.New(),
            ReportRequestId = request.Id,
            TraineeId = trainee.Id,
            PayloadJson = payloadJson
        };
        var measurement = new Measurement
        {
            Id = Id<Measurement>.New(),
            UserId = trainee.Id,
            BodyPart = BodyParts.Waist,
            Unit = MeasurementUnits.Centimeters.ToString(),
            Value = 87.125,
            CreatedAt = new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero)
        };

        dbContext.AddRange(trainer, trainee, template, request, submission, measurement);
        await dbContext.SaveChangesAsync();
        return new MigrationPreservationSeed(
            request.Id,
            submission.Id,
            measurement.Id,
            trainee.Id,
            payloadJson,
            measurement.BodyPart,
            measurement.Unit,
            measurement.Value,
            measurement.CreatedAt);
    }

    private static async Task AssertDuplicateRequestIsRejectedAsync(AppDbContext dbContext, RecurringRequestSeed seed)
    {
        dbContext.ReportRequests.Add(CreateRequest(seed, "duplicate"));

        try
        {
            await dbContext.SaveChangesAsync();
            Assert.Fail("Expected the pre-repair unique index to reject a second recurring report request.");
        }
        catch (DbUpdateException exception)
        {
            var postgresException = exception.InnerException.Should().BeOfType<PostgresException>().Which;

            postgresException.SqlState.Should().Be("23505");
            postgresException.ConstraintName.Should().Be(RecurringAssignmentIndexName);
            TestContext.Progress.WriteLine(
                $"Pre-repair duplicate signature: SQLSTATE {postgresException.SqlState}; constraint {postgresException.ConstraintName}");
        }
        finally
        {
            dbContext.ChangeTracker.Clear();
        }
    }

    private static User CreateUser(string prefix)
    {
        var suffix = Id<User>.New();
        return new User
        {
            Id = Id<User>.New(),
            Name = $"{prefix}-{suffix}",
            Email = $"{prefix}-{suffix}@test.local",
            ProfileRank = "Rookie"
        };
    }

    private static ReportRequest CreateRequest(RecurringRequestSeed seed, string note)
        => new()
        {
            Id = Id<ReportRequest>.New(),
            TrainerId = seed.TrainerId,
            TraineeId = seed.TraineeId,
            TemplateId = seed.TemplateId,
            RecurringReportAssignmentId = seed.AssignmentId,
            Status = ReportRequestStatus.Pending,
            DueAt = DateTimeOffset.UtcNow.AddDays(7),
            Note = note
        };

    private sealed record RecurringRequestSeed(
        Id<User> TrainerId,
        Id<User> TraineeId,
        Id<ReportTemplate> TemplateId,
        Id<RecurringReportAssignment> AssignmentId);

    private sealed record MigrationPreservationSeed(
        Id<ReportRequest> RequestId,
        Id<ReportSubmission> SubmissionId,
        Id<Measurement> MeasurementId,
        Id<User> TraineeId,
        string PayloadJson,
        BodyParts BodyPart,
        string Unit,
        double Value,
        DateTimeOffset CreatedAtUtc);
}
