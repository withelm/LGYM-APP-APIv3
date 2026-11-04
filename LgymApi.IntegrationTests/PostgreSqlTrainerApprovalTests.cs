using FluentAssertions;
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
        (await firstTask).IsSuccess.Should().BeTrue();
        second.Release.SetResult();
        var losingResult = await secondTask;
        losingResult.IsFailure.Should().BeTrue();
        losingResult.Error.HttpStatusCode.Should().Be(409);
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

        await using (var rejection = Factory.Services.CreateAsyncScope())
        {
            var service = rejection.ServiceProvider.GetRequiredService<ITrainerApprovalService>();
            (await service.DecideAsync(admin, target, TrainerApprovalStatus.Rejected,
                cancellationToken: timeout.Token)).IsSuccess.Should().BeTrue();
        }
        await using (var submission = Factory.Services.CreateAsyncScope())
        {
            var provider = submission.ServiceProvider;
            var db = provider.GetRequiredService<AppDbContext>();
            var session = new UserSession { Id = Id<UserSession>.New(), UserId = applicant.Id,
                Jti = "resubmission-test", ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1) };
            db.UserSessions.Add(session);
            await db.SaveChangesAsync(timeout.Token);
            var actor = await provider.GetRequiredService<IAuthenticatedAccountContextResolver>()
                .ResolveAsync(target, session.Id.Rebind<AccountSessionReference>(), timeout.Token);
            var service = provider.GetRequiredService<ITrainerApprovalService>();
            (await service.ResubmitAsync(actor.Context!, timeout.Token)).IsSuccess.Should().BeTrue();
        }

        stale.Release.SetResult();
        (await staleDecision).Error.HttpStatusCode.Should().Be(409);
        await AssertStateAsync(applicant.Id, TrainerApprovalStatus.PendingApproval, null);
        await using var verify = Factory.Services.CreateAsyncScope();
        var current = await verify.ServiceProvider.GetRequiredService<AppDbContext>().Users
            .SingleAsync(user => user.Id == applicant.Id, timeout.Token);
        current.TrainerApplicationAttempt.Should().Be(2);
        current.TrainerRejectionHistory.Should().Contain(admin.Id.ToString());
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
        await action.Should().ThrowAsync<InvalidOperationException>();
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
            approvalIndex.Should().BeGreaterThan(0);
            await before.GetService<IMigrator>().MigrateAsync(migrations[approvalIndex - 1]);
            foreach (var (id, name, deleted) in new[] { (trainerId, "legacy-trainer", false), (athleteId, "athlete", false), (deletedId, "deleted", true) })
            {
                await before.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Users" ("Id", "Name", "Email", "PreferredLanguage", "PreferredTimeZone", "ProfileRank", "IsVisibleInRanking", "IsBlocked", "IsDeleted", "CreatedAt", "UpdatedAt")
                    VALUES ({id.Value}, {name}, {name + "@example.test"}, 'en', 'UTC', 'Junior 1', TRUE, FALSE, {deleted}, {created}, {created})
                    """);
            }
            foreach (var id in new[] { trainerId, deletedId })
                await before.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "UserRoles" ("UserId", "RoleId") VALUES ({id.Value}, {RoleSeedDataConfiguration.TrainerRoleSeedId.Value})
                    """);
            await before.Database.MigrateAsync();
        }
        await using (var after = new AppDbContext(options))
        {
            after.Database.HasPendingModelChanges().Should().BeFalse();
            var trainer = await after.Users.SingleAsync(user => user.Id == trainerId);
            trainer.TrainerApprovalStatus.Should().Be(TrainerApprovalStatus.Approved);
            trainer.TrainerApplicationCreatedAt.Should().Be(created);
            trainer.TrainerApprovalReviewedBy.Should().BeNull();
            trainer.TrainerApprovalDecidedAt.Should().NotBeNull();
            (await after.Users.SingleAsync(user => user.Id == athleteId)).TrainerApprovalStatus.Should().BeNull();
            (await after.Users.IgnoreQueryFilters().SingleAsync(user => user.Id == deletedId)).TrainerApprovalStatus.Should().BeNull();
            after.Users.Add(new User { Id = Id<User>.New(), Name = "new-pending", Email = "new-pending@example.test",
                TrainerApprovalStatus = TrainerApprovalStatus.PendingApproval, TrainerApplicationCreatedAt = DateTimeOffset.UtcNow });
            await after.SaveChangesAsync();
            await after.Database.MigrateAsync();
        }
        await using var verify = new AppDbContext(options);
        (await verify.Users.SingleAsync(user => user.Name == "new-pending")).TrainerApprovalStatus.Should().Be(TrainerApprovalStatus.PendingApproval);
        (await verify.UserRoles.CountAsync()).Should().Be(2);
    }

    private async Task<(User Applicant, AuthenticatedAccountContext Actor)> SeedScenarioAsync()
    {
        var admin = await SeedAdminAsync();
        var applicant = await SeedUserAsync("applicant", "applicant@example.test");
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pending = await db.Users.SingleAsync(user => user.Id == applicant.Id);
        pending.TrainerApprovalStatus = TrainerApprovalStatus.PendingApproval;
        pending.TrainerApplicationCreatedAt = DateTimeOffset.UtcNow;
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
        var actual = await db.Users.AsNoTracking().SingleAsync(user => user.Id == applicant);
        actual.TrainerApprovalStatus.Should().Be(expected);
        actual.TrainerApprovalReviewedBy.Should().Be(reviewer);
        actual.TrainerApprovalDecidedAt.HasValue.Should().Be(reviewer.HasValue);
        (await db.UserRoles.CountAsync(role => role.UserId == applicant && role.RoleId == RoleSeedDataConfiguration.TrainerRoleSeedId))
            .Should().Be(expected == TrainerApprovalStatus.Approved ? 1 : 0);
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
        public Task<LgymApi.Application.Pagination.Pagination<User>> ListAsync(int page, int pageSize,
            bool includeReviewed, CancellationToken cancellationToken) => inner.ListAsync(page, pageSize, includeReviewed, cancellationToken);

        public async Task<User?> FindAsync(Id<User> id, bool forUpdate, CancellationToken cancellationToken)
        {
            var user = await inner.FindAsync(id, forUpdate, cancellationToken);
            loaded.SetResult();
            await release.Task.WaitAsync(cancellationToken);
            return user;
        }
    }

}
