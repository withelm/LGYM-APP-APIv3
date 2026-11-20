using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Nutrition.DietPlans.Models;
using LgymApi.Application.Nutrition.DietPlans.UpdateOwnDietPlan;
using LgymApi.Application.Nutrition.DietPlans.UpdateOwnDietPlan.Contracts;
using LgymApi.Application.Nutrition.DietPlans.UpdateTraineePlan;
using LgymApi.Application.Nutrition.DietPlans.UpdateTraineePlan.Contracts;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.Accounts;
using LgymApi.Identity.Contracts.Authorization;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.UnitOfWork;
using LgymApi.TestUtils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("PostgreSql")]
public sealed class PostgreSqlInheritedDietConcurrencyTests : PostgreSqlIntegrationTestBase
{
    [Test]
    public async Task UpdateInheritedDiet_CurrentCoachPreservesOriginalAuthorAndRecordsEditor()
    {
        var scenario = await SeedInheritedDietAsync(CancellationToken.None);
        var updateData = CreateUpdateData("Current coach edit", "Current coach meal");

        await using (var updateScope = Factory.Services.CreateAsyncScope())
        {
            var update = updateScope.ServiceProvider.GetRequiredService<IUpdateTraineeDietPlanUseCase>();
            var authorization = updateScope.ServiceProvider.GetRequiredService<IAuthenticatedAccountAuthorizationService>();
            var result = await update.ExecuteAsync(new UpdateTraineeDietPlanCommand(
                authorization.CreateActor(CreateAccountContext(scenario.CurrentCoach.Id)),
                scenario.CurrentCoach.Id,
                scenario.Trainee.Id,
                scenario.PlanId,
                updateData));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.TrainerId, Is.EqualTo(scenario.OriginalCoach.Id));
        }

        await using var verificationScope = Factory.Services.CreateAsyncScope();
        var database = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plan = await database.DietPlans
            .AsNoTracking()
            .Include(candidate => candidate.Meals)
            .SingleAsync(candidate => candidate.Id == scenario.PlanId);
        var history = await database.DietPlanHistories
            .AsNoTracking()
            .Where(entry => entry.DietPlanId == scenario.PlanId)
            .OrderBy(entry => entry.ChangeDate)
            .ToListAsync();

        Assert.That(plan.TrainerId, Is.EqualTo(scenario.OriginalCoach.Id));
        Assert.That(plan.Name, Is.EqualTo(updateData.Name));
        Assert.That((plan.Meals).Where(meal => meal.Name == updateData.Meals[0].Name), Has.Exactly(1).Items);
        Assert.That(history, Has.Exactly(2).Items);
        Assert.That(history.Single(entry => entry.ChangeType == "Updated").ChangedByUserId, Is.EqualTo(scenario.CurrentCoach.Id));
    }

    [Test]
    public async Task UpdateInheritedDiet_OwnerAndCurrentCoachCompete_StaleWriteLeavesNoPartialDataOrAudit()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancellationToken = timeout.Token;
        var scenario = await SeedInheritedDietAsync(cancellationToken);
        var barrier = new SaveBarrier();
        using var raceFactory = Factory.WithWebHostBuilder(builder => ConfigureSaveBarrier(builder, barrier));
        await using var ownerScope = raceFactory.Services.CreateAsyncScope();
        await using var coachScope = raceFactory.Services.CreateAsyncScope();
        var ownerUpdate = ownerScope.ServiceProvider.GetRequiredService<IUpdateOwnDietPlanUseCase>();
        var coachUpdate = coachScope.ServiceProvider.GetRequiredService<IUpdateTraineeDietPlanUseCase>();
        var ownerAuthorization = ownerScope.ServiceProvider.GetRequiredService<IAuthenticatedAccountAuthorizationService>();
        var coachAuthorization = coachScope.ServiceProvider.GetRequiredService<IAuthenticatedAccountAuthorizationService>();
        var ownerData = CreateUpdateData("Owner winning diet", "Owner meal");
        var coachData = CreateUpdateData("Coach winning diet", "Coach meal");

        var outcomes = await Task.WhenAll(
            CaptureAsync(
                scenario.Trainee.Id,
                ownerData.Name,
                ownerData.Meals[0].Name,
                () => ownerUpdate.ExecuteAsync(
                    new UpdateOwnDietPlanCommand(
                        ownerAuthorization.CreateActor(CreateAccountContext(scenario.Trainee.Id)),
                        scenario.Trainee.Id,
                        scenario.PlanId,
                        ownerData),
                    cancellationToken)),
            CaptureAsync(
                scenario.CurrentCoach.Id,
                coachData.Name,
                coachData.Meals[0].Name,
                () => coachUpdate.ExecuteAsync(
                    new UpdateTraineeDietPlanCommand(
                        coachAuthorization.CreateActor(CreateAccountContext(scenario.CurrentCoach.Id)),
                        scenario.CurrentCoach.Id,
                        scenario.Trainee.Id,
                        scenario.PlanId,
                        coachData),
                    cancellationToken))).WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);

        Assert.That((outcomes).Where(outcome => outcome.Succeeded && outcome.Exception == null), Has.Exactly(1).Items);
        Assert.That((outcomes).Where(outcome => outcome.Exception is DbUpdateConcurrencyException), Has.Exactly(1).Items);
        Assert.That((outcomes).All(outcome => outcome.ApplicationError == null), Is.True);
        var winner = outcomes.Single(outcome => outcome.Succeeded);
        var loser = outcomes.Single(outcome => outcome.Exception is not null);

        await using var verificationScope = Factory.Services.CreateAsyncScope();
        var database = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plan = await database.DietPlans
            .AsNoTracking()
            .Include(candidate => candidate.Meals)
            .SingleAsync(candidate => candidate.Id == scenario.PlanId, cancellationToken);
        var history = await database.DietPlanHistories
            .AsNoTracking()
            .Where(entry => entry.DietPlanId == scenario.PlanId)
            .OrderBy(entry => entry.ChangeDate)
            .ToListAsync(cancellationToken);

        Assert.That(plan.TrainerId, Is.EqualTo(scenario.OriginalCoach.Id));
        Assert.That(plan.Name, Is.EqualTo(winner.PlanName));
        Assert.That((plan.Meals).Where(meal => meal.Name == winner.MealName), Has.Exactly(1).Items);
        Assert.That((plan.Meals).Any(meal => meal.Name == loser.MealName), Is.False);
        Assert.That(history, Has.Exactly(2).Items);
        var updateHistory = history.Single(entry => entry.ChangeType == "Updated");
        Assert.That(updateHistory.ChangedByUserId, Is.EqualTo(winner.ActorId));
        Assert.That(updateHistory.SnapshotJson, Does.Contain(winner.PlanName));
        Assert.That(updateHistory.SnapshotJson, Does.Not.Contain(loser.PlanName));

        TestContext.Progress.WriteLine(
            $"winner={winner.ActorId}; loser={loser.ActorId}; conflict={loser.Exception!.GetType().Name}; "
            + $"originalAuthorPreserved={plan.TrainerId == scenario.OriginalCoach.Id}; "
            + $"persistedMeals={plan.Meals.Count}; persistedHistory={history.Count}");
    }

    [Test]
    public async Task UpdateInheritedDiet_WhenOnlyMealsDiffer_StillRejectsOneConcurrentAggregateWriter()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancellationToken = timeout.Token;
        var scenario = await SeedInheritedDietAsync(cancellationToken);
        var barrier = new SaveBarrier();
        using var raceFactory = Factory.WithWebHostBuilder(builder => ConfigureSaveBarrier(builder, barrier));
        await using var ownerScope = raceFactory.Services.CreateAsyncScope();
        await using var coachScope = raceFactory.Services.CreateAsyncScope();
        var ownerUpdate = ownerScope.ServiceProvider.GetRequiredService<IUpdateOwnDietPlanUseCase>();
        var coachUpdate = coachScope.ServiceProvider.GetRequiredService<IUpdateTraineeDietPlanUseCase>();
        var ownerAuthorization = ownerScope.ServiceProvider.GetRequiredService<IAuthenticatedAccountAuthorizationService>();
        var coachAuthorization = coachScope.ServiceProvider.GetRequiredService<IAuthenticatedAccountAuthorizationService>();
        var ownerData = CreateMealOnlyUpdateData("Owner replacement meal");
        var coachData = CreateMealOnlyUpdateData("Coach replacement meal");

        var outcomes = await Task.WhenAll(
            CaptureAsync(
                scenario.Trainee.Id,
                ownerData.Name,
                ownerData.Meals[0].Name,
                () => ownerUpdate.ExecuteAsync(
                    new UpdateOwnDietPlanCommand(
                        ownerAuthorization.CreateActor(CreateAccountContext(scenario.Trainee.Id)),
                        scenario.Trainee.Id,
                        scenario.PlanId,
                        ownerData),
                    cancellationToken)),
            CaptureAsync(
                scenario.CurrentCoach.Id,
                coachData.Name,
                coachData.Meals[0].Name,
                () => coachUpdate.ExecuteAsync(
                    new UpdateTraineeDietPlanCommand(
                        coachAuthorization.CreateActor(CreateAccountContext(scenario.CurrentCoach.Id)),
                        scenario.CurrentCoach.Id,
                        scenario.Trainee.Id,
                        scenario.PlanId,
                        coachData),
                    cancellationToken))).WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);

        Assert.That((outcomes).Where(outcome => outcome.Succeeded && outcome.Exception == null), Has.Exactly(1).Items);
        Assert.That((outcomes).Where(outcome => outcome.Exception is DbUpdateConcurrencyException), Has.Exactly(1).Items);
        Assert.That((outcomes).All(outcome => outcome.ApplicationError == null), Is.True);
        var winner = outcomes.Single(outcome => outcome.Succeeded);
        var loser = outcomes.Single(outcome => outcome.Exception is not null);

        await using var verificationScope = Factory.Services.CreateAsyncScope();
        var database = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plan = await database.DietPlans
            .AsNoTracking()
            .Include(candidate => candidate.Meals)
            .SingleAsync(candidate => candidate.Id == scenario.PlanId, cancellationToken);
        var history = await database.DietPlanHistories
            .AsNoTracking()
            .Where(entry => entry.DietPlanId == scenario.PlanId)
            .OrderBy(entry => entry.ChangeDate)
            .ToListAsync(cancellationToken);

        Assert.That(plan.Name, Is.EqualTo("Original inherited diet"));
        Assert.That((plan.Meals).Where(meal => meal.Name == winner.MealName), Has.Exactly(1).Items);
        Assert.That((plan.Meals).Any(meal => meal.Name == loser.MealName), Is.False);
        Assert.That(history, Has.Exactly(2).Items);
        var updateHistory = history.Single(entry => entry.ChangeType == "Updated");
        Assert.That(updateHistory.ChangedByUserId, Is.EqualTo(winner.ActorId));
        Assert.That(updateHistory.SnapshotJson, Does.Contain(winner.MealName));
        Assert.That(updateHistory.SnapshotJson, Does.Not.Contain(loser.MealName));
    }

    private async Task<RaceScenario> SeedInheritedDietAsync(CancellationToken cancellationToken)
    {
        var suffix = Id<PostgreSqlInheritedDietConcurrencyTests>.New().ToString().Replace("-", string.Empty, StringComparison.Ordinal);
        await using var scope = Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var originalCoach = await TestDataFactory.SeedUserAsync(
            database,
            $"diet-original-{suffix}",
            $"diet-original-{suffix}@example.test",
            cancellationToken: cancellationToken);
        var currentCoach = await TestDataFactory.SeedUserAsync(
            database,
            $"diet-current-{suffix}",
            $"diet-current-{suffix}@example.test",
            cancellationToken: cancellationToken);
        var trainee = await TestDataFactory.SeedUserAsync(
            database,
            $"diet-trainee-{suffix}",
            $"diet-trainee-{suffix}@example.test",
            cancellationToken: cancellationToken);
        var plan = new DietPlan
        {
            Id = Id<DietPlan>.New(),
            TrainerId = originalCoach.Id,
            TraineeId = trainee.Id,
            Name = "Original inherited diet",
            StartDate = new DateOnly(2026, 11, 1),
            IsActive = true
        };
        database.TrainerTraineeLinks.Add(new TrainerTraineeLink
        {
            Id = Id<TrainerTraineeLink>.New(),
            TrainerId = currentCoach.Id,
            TraineeId = trainee.Id
        });
        database.UserRoles.AddRange(
            TestDataFactory.ApprovedTrainerRole(database, originalCoach.Id),
            TestDataFactory.ApprovedTrainerRole(database, currentCoach.Id));
        database.DietPlans.Add(plan);
        database.DietPlanHistories.Add(new DietPlanHistory
        {
            Id = Id<DietPlanHistory>.New(),
            DietPlanId = plan.Id,
            ChangedByUserId = originalCoach.Id,
            ChangeDate = DateTimeOffset.UtcNow,
            ChangeType = "Created",
            SnapshotJson = "{\"Name\":\"Original inherited diet\",\"Meals\":[]}"
        });
        await database.SaveChangesAsync(cancellationToken);

        return new RaceScenario(originalCoach, currentCoach, trainee, plan.Id);
    }

    private static DietPlanUpsertData CreateUpdateData(string planName, string mealName)
        => new(
            planName,
            new DateOnly(2026, 11, 2),
            new DateOnly(2026, 12, 31),
            2400,
            180,
            250,
            70,
            $"Contested update by {planName}",
            true,
            [new DietMealInput(mealName, 0, "Race-specific meal", 600, 45, 60, 20)]);

    private static DietPlanUpsertData CreateMealOnlyUpdateData(string mealName)
        => new(
            "Original inherited diet",
            new DateOnly(2026, 11, 1),
            null,
            null,
            null,
            null,
            null,
            null,
            true,
            [new DietMealInput(mealName, 0, "Race-specific meal", 600, 45, 60, 20)]);

    private static AuthenticatedAccountContext CreateAccountContext(Id<User> userId)
        => new(
            userId.Rebind<AccountReference>(),
            null,
            [],
            [],
            IsBlocked: false,
            IsDeleted: false);

    private static async Task<UpdateOutcome> CaptureAsync(
        Id<User> actorId,
        string planName,
        string mealName,
        Func<Task<Result<DietPlanReadModel, AppError>>> operation)
    {
        try
        {
            var result = await operation();
            return new UpdateOutcome(
                actorId,
                planName,
                mealName,
                result.IsSuccess,
                result.IsFailure ? result.Error : null,
                null);
        }
        catch (Exception exception)
        {
            return new UpdateOutcome(actorId, planName, mealName, false, null, exception);
        }
    }

    private static void ConfigureSaveBarrier(IWebHostBuilder builder, SaveBarrier barrier)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork>(provider =>
            {
                var database = provider.GetRequiredService<AppDbContext>();
                return new BarrierUnitOfWork(new EfUnitOfWork(database), barrier);
            });
        });
    }

    private sealed record RaceScenario(
        User OriginalCoach,
        User CurrentCoach,
        User Trainee,
        Id<DietPlan> PlanId);

    private sealed record UpdateOutcome(
        Id<User> ActorId,
        string PlanName,
        string MealName,
        bool Succeeded,
        AppError? ApplicationError,
        Exception? Exception);

    private sealed class SaveBarrier
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrivals) == 2)
            {
                _release.TrySetResult();
            }

            await _release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }

    private sealed class BarrierUnitOfWork(IUnitOfWork inner, SaveBarrier barrier) : IUnitOfWork
    {
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await barrier.WaitAsync(cancellationToken);
            return await inner.SaveChangesAsync(cancellationToken);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => inner.BeginTransactionAsync(cancellationToken);
    }
}
