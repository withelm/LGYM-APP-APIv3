using LgymApi.Application.Mapping.Core;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Approval;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Approval;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Data.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LgymApi.IntegrationTests;

[TestFixture, NonParallelizable, Category("PostgreSql")]
public sealed class PostgreSqlTrainerApprovalTests : PostgreSqlIntegrationTestBase
{
    [TestCase(TrainerApprovalStatus.Approved, TrainerApprovalStatus.Rejected)]
    [TestCase(TrainerApprovalStatus.Rejected, TrainerApprovalStatus.Approved)]
    [TestCase(TrainerApprovalStatus.Approved, TrainerApprovalStatus.Approved)]
    public async Task ConcurrentDecisions_OnlyOneDecisionAndItsRoleCommit(TrainerApprovalStatus winner, TrainerApprovalStatus loser)
    {
        var (applicant, actor) = await SeedScenarioAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var firstScope = Factory.Services.CreateAsyncScope();
        await using var secondScope = Factory.Services.CreateAsyncScope();
        var first = CreateGatedService(firstScope.ServiceProvider);
        var second = CreateGatedService(secondScope.ServiceProvider);
        var firstTask = first.Service.DecideAsync(actor, applicant.Id.Rebind<AccountReference>(), winner, cancellationToken: timeout.Token);
        var secondTask = second.Service.DecideAsync(actor, applicant.Id.Rebind<AccountReference>(), loser, cancellationToken: timeout.Token);
        await Task.WhenAll(first.Loaded.Task, second.Loaded.Task).WaitAsync(timeout.Token);
        first.Release.SetResult();
        Assert.That((await firstTask).IsSuccess, Is.True);
        second.Release.SetResult();
        var losingResult = await secondTask;
        Assert.That(losingResult.IsFailure, Is.True);
        Assert.That(losingResult.Error.HttpStatusCode, Is.EqualTo(409));
        await AssertStateAsync(applicant.Id, winner, actor.Id.Rebind<User>());
    }

    [Test]
    public async Task DecisionLoadedBeforeResubmission_CannotApproveNewAttempt()
    {
        var (applicant, admin) = await SeedScenarioAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var staleScope = Factory.Services.CreateAsyncScope();
        var stale = CreateGatedService(staleScope.ServiceProvider);
        var target = applicant.Id.Rebind<AccountReference>();
        var staleDecision = stale.Service.DecideAsync(admin, target, TrainerApprovalStatus.Approved,
            cancellationToken: timeout.Token);
        await stale.Loaded.Task.WaitAsync(timeout.Token);

        await RejectAsync(admin, target, 1, timeout.Token);
        var actor = await CreateApplicantSessionAsync(applicant.Id, timeout.Token);
        await using (var submission = Factory.Services.CreateAsyncScope())
        {
            var service = submission.ServiceProvider.GetRequiredService<ITrainerApprovalService>();
            Assert.That((await service.ResubmitAsync(actor, timeout.Token)).IsSuccess, Is.True);
        }

        stale.Release.SetResult();
        Assert.That((await staleDecision).Error.HttpStatusCode, Is.EqualTo(409));
        await AssertStateAsync(applicant.Id, TrainerApprovalStatus.PendingApproval, null);
        await using var verify = Factory.Services.CreateAsyncScope();
        var history = await verify.ServiceProvider.GetRequiredService<AppDbContext>().TrainerApplications
            .Where(application => application.ApplicantId == applicant.Id)
            .OrderBy(application => application.Attempt).ToListAsync(timeout.Token);
        Assert.That(history, Has.Exactly(2).Items);
        Assert.That(history[0].Status, Is.EqualTo(TrainerApprovalStatus.Rejected));
        Assert.That(history[0].ReviewedBy, Is.EqualTo(admin.Id.Rebind<User>()));
        Assert.That(history[1].Attempt, Is.EqualTo(2));
    }

    [Test]
    public async Task ResubmissionLoadedBeforeAnotherRejection_DoesNotReopenTheNewerAttempt()
    {
        var (applicant, admin) = await SeedScenarioAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var target = applicant.Id.Rebind<AccountReference>();
        await RejectAsync(admin, target, 1, timeout.Token);
        var actor = await CreateApplicantSessionAsync(applicant.Id, timeout.Token);
        await using var staleScope = Factory.Services.CreateAsyncScope();
        var stale = CreateGatedService(staleScope.ServiceProvider);
        var staleSubmission = stale.Service.ResubmitAsync(actor, timeout.Token);
        await stale.Loaded.Task.WaitAsync(timeout.Token);

        await using (var submission = Factory.Services.CreateAsyncScope())
        {
            var service = submission.ServiceProvider.GetRequiredService<ITrainerApprovalService>();
            Assert.That((await service.ResubmitAsync(actor, timeout.Token)).IsSuccess, Is.True);
        }
        await RejectAsync(admin, target, 2, timeout.Token);
        stale.Release.SetResult();

        Assert.That((await staleSubmission).Error.HttpStatusCode, Is.EqualTo(409));
        await AssertStateAsync(applicant.Id, TrainerApprovalStatus.Rejected, admin.Id.Rebind<User>());
        await using var verify = Factory.Services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That((await db.TrainerApplications.CountAsync(application => application.ApplicantId == applicant.Id, timeout.Token)), Is.EqualTo(2));
    }

    [Test]
    public async Task FailureAfterDecisionSave_RollsBackDecisionAuditAndTrainerRole()
    {
        var (applicant, actor) = await SeedScenarioAsync();
        await using var scope = Factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var real = provider.GetRequiredService<IUnitOfWork>();
        var failing = Substitute.For<IUnitOfWork>();
        failing.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(call => real.BeginTransactionAsync(call.Arg<CancellationToken>()));
        var saves = 0;
        failing.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(call =>
            ++saves == 2 ? Task.FromException<int>(new InvalidOperationException("Injected role save failure"))
                : real.SaveChangesAsync(call.Arg<CancellationToken>()));
        var service = new TrainerApprovalService(provider.GetRequiredService<ITrainerApplicationRepository>(),
            provider.GetRequiredService<IRoleRepository>(), provider.GetRequiredService<IAuthenticatedAccountContextResolver>(),
            failing, provider.GetRequiredService<IMapper>());
        var action = () => service.DecideAsync(actor, applicant.Id.Rebind<AccountReference>(), TrainerApprovalStatus.Approved);
        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<InvalidOperationException>());
        await AssertStateAsync(applicant.Id, TrainerApprovalStatus.PendingApproval, null);
    }

    [Test]
    public async Task Migration_OnlyGrandfathersExistingTrainers_AndRerunPreservesNewPendingApplications()
    {
        await using var lease = await PostgreSqlDatabaseLease.CreateAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(lease.ConnectionString).Options;
        var trainerId = Id<User>.New();
        var athleteId = Id<User>.New();
        var deletedId = Id<User>.New();
        var created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using (var before = new AppDbContext(options))
        {
            var migrations = before.Database.GetMigrations().ToList();
            var approvalIndex = migrations.FindIndex(id => id.EndsWith("_AddTrainerApproval", StringComparison.Ordinal));
            Assert.That(approvalIndex, Is.GreaterThan(0));
            await before.GetService<IMigrator>().MigrateAsync(migrations[approvalIndex - 1]);
            foreach (var (id, name, deleted) in new[] { (trainerId, "legacy-trainer", false), (athleteId, "athlete", false), (deletedId, "deleted", true) })
            {
                await before.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Users" ("Id", "Name", "Email", "PreferredLanguage", "PreferredTimeZone", "ProfileRank", "IsVisibleInRanking", "IsBlocked", "IsDeleted", "CreatedAt", "UpdatedAt")
                    VALUES (CAST({id.ToString()} AS uuid), {name}, {name + "@example.test"}, 'en', 'UTC', 'Junior 1', TRUE, FALSE, {deleted}, {created}, {created})
                    """);
            }
            foreach (var id in new[] { trainerId, deletedId })
                await before.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "UserRoles" ("UserId", "RoleId") VALUES (CAST({id.ToString()} AS uuid), CAST({RoleSeedDataConfiguration.TrainerRoleSeedId.ToString()} AS uuid))
                    """);
            await before.Database.MigrateAsync();
        }
        await using (var after = new AppDbContext(options))
        {
            Assert.That(after.Database.HasPendingModelChanges(), Is.False);
            Assert.That(after.Model.FindEntityType(typeof(User))!.FindProperty("TrainerApprovalStatus"), Is.Null);
            var legacy = await after.TrainerApplications.SingleAsync(application => application.ApplicantId == trainerId);
            Assert.That(legacy.Attempt, Is.EqualTo(1));
            Assert.That(legacy.Status, Is.EqualTo(TrainerApprovalStatus.Approved));
            Assert.That(legacy.CreatedAt, Is.EqualTo(created));
            Assert.That(legacy.ReviewedBy, Is.Null);
            Assert.That(legacy.DecidedAt, Is.Not.Null);
            Assert.That((await after.TrainerApplications.AnyAsync(application =>
                application.ApplicantId == athleteId || application.ApplicantId == deletedId)), Is.False);
            var pending = new User { Id = Id<User>.New(), Name = "new-pending", Email = "new-pending@example.test" };
            pending.TrainerApplications.Add(TrainerApplicationAttempt.Submit(pending.Id, 1));
            after.Users.Add(pending);
            await after.SaveChangesAsync();
            await after.Database.MigrateAsync();
        }
        await using var verify = new AppDbContext(options);
        Assert.That((await verify.TrainerApplications.SingleAsync(application => application.Applicant.Name == "new-pending"))
            .Status, Is.EqualTo(TrainerApprovalStatus.PendingApproval));
        Assert.That((await verify.TrainerApplications.CountAsync()), Is.EqualTo(2));
        Assert.That((await verify.UserRoles.CountAsync()), Is.EqualTo(2));
    }

    private async Task<(User Applicant, AuthenticatedAccountContext Actor)> SeedScenarioAsync()
    {
        var admin = await SeedAdminAsync();
        var applicant = await SeedUserAsync("applicant", "applicant@example.test");
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.TrainerApplications.Add(TrainerApplicationAttempt.Submit(applicant.Id, 1));
        var session = new UserSession { Id = Id<UserSession>.New(), UserId = admin.Id, Jti = "approval-test", ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1) };
        db.UserSessions.Add(session);
        await db.SaveChangesAsync();
        var resolved = await scope.ServiceProvider.GetRequiredService<IAuthenticatedAccountContextResolver>()
            .ResolveAsync(admin.Id.Rebind<AccountReference>(), session.Id.Rebind<AccountSessionReference>());
        return (applicant, resolved.Context!);
    }

    private async Task AssertStateAsync(Id<User> applicant, TrainerApprovalStatus expected, Id<User>? reviewer)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var latest = await db.TrainerApplications.Where(item => item.ApplicantId == applicant)
            .OrderByDescending(item => item.Attempt).FirstAsync();
        Assert.That(latest.Status, Is.EqualTo(expected));
        Assert.That(latest.ReviewedBy, Is.EqualTo(reviewer));
        Assert.That(latest.DecidedAt.HasValue, Is.EqualTo(reviewer.HasValue));
        Assert.That((await db.UserRoles.CountAsync(role => role.UserId == applicant && role.RoleId == RoleSeedDataConfiguration.TrainerRoleSeedId)), Is.EqualTo(expected == TrainerApprovalStatus.Approved ? 1 : 0));
    }

    private async Task<AuthenticatedAccountContext> CreateApplicantSessionAsync(Id<User> applicant, CancellationToken cancellationToken)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<AppDbContext>();
        var session = new UserSession { Id = Id<UserSession>.New(), UserId = applicant,
            Jti = "resubmission-test", ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1) };
        db.UserSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        var resolution = await provider.GetRequiredService<IAuthenticatedAccountContextResolver>()
            .ResolveAsync(applicant.Rebind<AccountReference>(), session.Id.Rebind<AccountSessionReference>(), cancellationToken);
        return resolution.Context!;
    }

    private async Task RejectAsync(AuthenticatedAccountContext admin, Id<AccountReference> target, int attempt, CancellationToken cancellationToken)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ITrainerApprovalService>();
        Assert.That((await service.DecideAsync(admin, target, TrainerApprovalStatus.Rejected, attempt, cancellationToken))
            .IsSuccess, Is.True);
    }

    private static (TrainerApprovalService Service, TaskCompletionSource Loaded, TaskCompletionSource Release) CreateGatedService(IServiceProvider provider)
    {
        var real = provider.GetRequiredService<ITrainerApplicationRepository>();
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gated = new GatedRepository(real, loaded, release);
        return (new TrainerApprovalService(gated, provider.GetRequiredService<IRoleRepository>(),
            provider.GetRequiredService<IAuthenticatedAccountContextResolver>(), provider.GetRequiredService<IUnitOfWork>(),
            provider.GetRequiredService<IMapper>()), loaded, release);
    }
    private sealed class GatedRepository(ITrainerApplicationRepository inner, TaskCompletionSource loaded,
        TaskCompletionSource release) : ITrainerApplicationRepository
    {
        public Task<LgymApi.Application.Pagination.Pagination<TrainerApplicationAttempt>> ListAsync(int page, int pageSize,
            bool includeReviewed, CancellationToken cancellationToken) => inner.ListAsync(page, pageSize, includeReviewed, cancellationToken);

        public async Task<TrainerApplicationAttempt?> FindLatestAsync(Id<User> id, bool forUpdate, CancellationToken cancellationToken)
        {
            var application = await inner.FindLatestAsync(id, forUpdate, cancellationToken);
            if (forUpdate)
            {
                loaded.SetResult();
                await release.Task.WaitAsync(cancellationToken);
            }
            return application;
        }

        public void Add(TrainerApplicationAttempt application) => inner.Add(application);

        public Task<TrainerApprovalStatus?> GetLatestStatusAsync(Id<User> id, CancellationToken cancellationToken) =>
            inner.GetLatestStatusAsync(id, cancellationToken);
    }

}
