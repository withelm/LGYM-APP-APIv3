using System.Data.Common;
using System.Text.RegularExpressions;
using FluentAssertions;
using LgymApi.Application.Coaching.ManagedPlans.Assign;
using LgymApi.Application.Coaching.ManagedPlans.Unassign;
using LgymApi.Application.TrainingPlanning.Plan.CreatePlan;
using LgymApi.Application.TrainingPlanning.Plan.SetActivePlan;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Infrastructure.Data;
using LgymApi.TestUtils;
using LgymApi.TrainingPlanning.Contracts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("PostgreSql")]
public sealed class PostgreSqlPlanSelectionRaceTests : PostgreSqlIntegrationTestBase
{
    [TestCase(true, true, true)]
    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    [TestCase(false, false, true)]
    [TestCase(true, true, false)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, false)]
    public async Task CoachSelectionCannotDeactivatePrivatePlanSelectedAfterItsActivePlanRead(bool assign, bool initiallyActive, bool createPrivate)
    {
        Id<User> ownerId;
        Id<User> coachId;
        var personalId = Id<Plan>.New();
        var templateId = Id<Plan>.New();
        var privateId = Id<Plan>.New();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = await TestDataFactory.SeedUserAsync(db, "selection-owner", "selection-owner@example.test");
            var coach = await TestDataFactory.SeedUserAsync(db, "selection-coach", "selection-coach@example.test");
            ownerId = owner.Id;
            coachId = coach.Id;
            db.UserRoles.Add(TestDataFactory.ApprovedTrainerRole(db, coachId));
            db.TrainerTraineeLinks.Add(new TrainerTraineeLink
            {
                Id = Id<TrainerTraineeLink>.New(), TrainerId = coachId, TraineeId = ownerId
            });
            db.Plans.AddRange(
                new Plan { Id = personalId, UserId = ownerId, Name = "Existing personal", Purpose = PlanPurpose.Personal, IsActive = initiallyActive },
                new Plan { Id = templateId, UserId = coachId, Name = "Coach template", Purpose = PlanPurpose.Professional, IsActive = false });
            if (!createPrivate)
            {
                db.Plans.Add(new Plan { Id = privateId, UserId = ownerId, Name = "New private plan", Purpose = PlanPurpose.Unclassified, IsActive = false });
            }
            await db.SaveChangesAsync();
        }

        var gate = new ActivePlanReadGate();
        using var gatedFactory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddDbContext<AppDbContext>((_, options) => options.AddInterceptors(gate))));
        await using var coachScope = gatedFactory.Services.CreateAsyncScope();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var ct = timeout.Token;
        var ownerAccount = ownerId.Rebind<AccountReference>();
        var coachAccount = coachId.Rebind<AccountReference>();
        var mutation = assign
            ? coachScope.ServiceProvider.GetRequiredService<IAssignTraineeManagedPlanUseCase>().ExecuteAsync(
                new AssignTraineeManagedPlanCommand(coachAccount, ownerAccount, templateId.Rebind<PlanReference>()), ct)
            : coachScope.ServiceProvider.GetRequiredService<IUnassignTraineeManagedPlanUseCase>().ExecuteAsync(
                new UnassignTraineeManagedPlanCommand(coachAccount, ownerAccount), ct);

        try
        {
            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            await using (var ownerScope = Factory.Services.CreateAsyncScope())
            {
                var selected = createPrivate
                    ? await ownerScope.ServiceProvider.GetRequiredService<ICreatePlanUseCase>().ExecuteAsync(new CreatePlanCommand(
                        ownerAccount, ownerAccount, "New private plan", PlanPurpose.Unclassified), ct)
                    : await ownerScope.ServiceProvider.GetRequiredService<ISetActivePlanUseCase>().ExecuteAsync(new SetActivePlanCommand(
                        ownerAccount, ownerAccount, privateId.Rebind<PlanReference>()), ct);
                selected.IsSuccess.Should().BeTrue();
            }
            gate.Release.TrySetResult();
            try
            {
                await mutation.WaitAsync(TimeSpan.FromSeconds(15), ct);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
            {
                exception.SqlState.Should().Be(PostgresErrorCodes.SerializationFailure);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
            {
                exception.InnerException.Should().BeOfType<PostgresException>();
            }
            catch (InvalidOperationException exception) when (exception.InnerException is DbUpdateException
                { InnerException: PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } })
            {
                exception.GetBaseException().Should().BeOfType<PostgresException>();
            }
        }
        finally
        {
            gate.Release.TrySetResult();
        }

        await using var verification = Factory.Services.CreateAsyncScope();
        var database = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var activePlans = await database.Plans.AsNoTracking().Where(plan => plan.UserId == ownerId && plan.IsActive).ToListAsync(ct);
        activePlans.Should().ContainSingle();
        activePlans[0].Name.Should().Be("New private plan");
        activePlans[0].Purpose.Should().Be(PlanPurpose.Unclassified);
        (await database.Plans.AsNoTracking().SingleAsync(plan => plan.Id == personalId, ct)).IsActive.Should().BeFalse();
        (await database.Plans.CountAsync(plan => plan.UserId == ownerId, ct)).Should().Be(2);
    }

    private sealed class ActivePlanReadGate : DbCommandInterceptor
    {
        private int _captured;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM \"Plans\"", StringComparison.Ordinal)
                && Regex.IsMatch(command.CommandText, "WHERE[\\s\\S]*\\.\"IsActive\"")
                && Interlocked.Exchange(ref _captured, 1) == 0)
            {
                Reached.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
}
