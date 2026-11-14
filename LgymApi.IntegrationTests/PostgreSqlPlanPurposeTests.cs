using FluentAssertions;
using LgymApi.Application.Repositories;
using LgymApi.Application.TrainingPlanning.Contracts.PlanDay;
using LgymApi.Application.TrainingPlanning.Plan.UpdatePlan;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.UnitOfWork;
using LgymApi.TestUtils;
using LgymApi.TrainingPlanning.Contracts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LgymApi.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("PostgreSql")]
public sealed class PostgreSqlPlanPurposeTests : PostgreSqlIntegrationTestBase
{
    private const string PreviousMigration = "20261112192912_AddReportFirstFeedbackMarker";
    private const string PurposeMigration = "20261113232705_AddPlanPurpose";

    [Test]
    public async Task PurposeMigration_PreservesExistingPlanDayAndWorkoutGraph_OnUpgradeAndRerun()
    {
        var graph = await SeedGraphAsync();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var migrations = db.Database.GetMigrations().ToList();
            migrations.IndexOf(PreviousMigration).Should().Be(migrations.IndexOf(PurposeMigration) - 1);
            await db.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        }

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.GetService<IMigrator>().MigrateAsync(PurposeMigration);
            await db.Database.GetService<IMigrator>().MigrateAsync(PurposeMigration);
        }

        await using var verification = Factory.Services.CreateAsyncScope();
        var database = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var plan = await database.Plans.AsNoTracking().SingleAsync(p => p.Id == graph.PlanId);
        var day = await database.PlanDays.IgnoreQueryFilters().AsNoTracking().SingleAsync(d => d.Id == graph.DayId);
        var exercise = await database.PlanDayExercises.AsNoTracking().SingleAsync(e => e.PlanDayId == graph.DayId);
        var training = await database.Trainings.AsNoTracking().SingleAsync(t => t.Id == graph.TrainingId);
        var score = await database.ExerciseScores.AsNoTracking().SingleAsync(s => s.Id == graph.ScoreId);
        var link = await database.TrainingExerciseScores.AsNoTracking().SingleAsync(s => s.ExerciseScoreId == graph.ScoreId);
        plan.Purpose.Should().Be(PlanPurpose.Unclassified);
        plan.Name.Should().Be("Original plan");
        plan.UserId.Should().Be(graph.OwnerId);
        day.PlanId.Should().Be(graph.PlanId);
        day.Name.Should().Be("Original day");
        exercise.ExerciseId.Should().Be(graph.ExerciseId);
        exercise.Reps.Should().Be("8");
        training.TypePlanDayId.Should().Be(graph.DayId);
        score.TrainingId.Should().Be(graph.TrainingId);
        score.ExerciseId.Should().Be(graph.ExerciseId);
        score.Reps.Should().Be(8);
        link.TrainingId.Should().Be(graph.TrainingId);
        (await database.Database.GetAppliedMigrationsAsync()).Count(id => id == PurposeMigration).Should().Be(1);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task CoachDayDelete_AndOwnerClassification_SerializeWithoutLosingHistory(bool ownerFirst)
    {
        var graph = await SeedGraphAsync();
        var gate = new SaveGate();
        using var gatedFactory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork>(provider => new GatedUnitOfWork(
                new EfUnitOfWork(provider.GetRequiredService<AppDbContext>()), gate));
        }));
        await using var coachScope = gatedFactory.Services.CreateAsyncScope();
        var dayService = coachScope.ServiceProvider.GetRequiredService<IPlanDayService>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var deletion = dayService.DeleteAsync(new DeletePlanDayCommand(
            graph.CoachId.Rebind<AccountReference>(), graph.DayId.Rebind<PlanDayReference>()), timeout.Token);
        try
        {
            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15), timeout.Token);
            if (!ownerFirst)
            {
                gate.Release.TrySetResult();
                (await deletion.WaitAsync(TimeSpan.FromSeconds(15), timeout.Token)).IsSuccess.Should().BeTrue();
            }

            await using (var ownerScope = Factory.Services.CreateAsyncScope())
            {
                var update = ownerScope.ServiceProvider.GetRequiredService<IUpdatePlanUseCase>();
                var owner = graph.OwnerId.Rebind<AccountReference>();
                var result = await update.ExecuteAsync(new UpdatePlanCommand(
                    owner, owner, graph.PlanId.Rebind<PlanReference>(), "Original plan", PlanPurpose.Professional), timeout.Token);
                result.IsSuccess.Should().BeTrue();
            }

            if (ownerFirst)
            {
                gate.Release.TrySetResult();
                Func<Task> staleSave = async () => await deletion;
                await staleSave.Should().ThrowAsync<DbUpdateConcurrencyException>();
            }
        }
        finally
        {
            gate.Release.TrySetResult();
        }

        await using var verification = Factory.Services.CreateAsyncScope();
        var database = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        (await database.Plans.AsNoTracking().SingleAsync(p => p.Id == graph.PlanId)).Purpose.Should().Be(PlanPurpose.Professional);
        var day = await database.PlanDays.IgnoreQueryFilters().AsNoTracking().SingleAsync(d => d.Id == graph.DayId);
        day.IsDeleted.Should().Be(!ownerFirst);
        day.Name.Should().Be("Original day");
        (await database.PlanDayExercises.AsNoTracking().SingleAsync(e => e.PlanDayId == graph.DayId)).ExerciseId.Should().Be(graph.ExerciseId);
        (await database.Trainings.AsNoTracking().SingleAsync(t => t.Id == graph.TrainingId)).TypePlanDayId.Should().Be(graph.DayId);
        (await database.ExerciseScores.AsNoTracking().SingleAsync(s => s.Id == graph.ScoreId)).TrainingId.Should().Be(graph.TrainingId);
        (await database.TrainingExerciseScores.AsNoTracking().SingleAsync(s => s.ExerciseScoreId == graph.ScoreId)).TrainingId.Should().Be(graph.TrainingId);
    }

    private async Task<Graph> SeedGraphAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await TestDataFactory.SeedUserAsync(db, "purpose-owner", "purpose-owner@example.test");
        var coach = await TestDataFactory.SeedUserAsync(db, "purpose-coach", "purpose-coach@example.test");
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner.Id, Name = "Original plan", Purpose = PlanPurpose.Personal };
        var day = new PlanDay { Id = Id<PlanDay>.New(), PlanId = plan.Id, Name = "Original day" };
        var exercise = new Exercise { Id = Id<Exercise>.New(), UserId = owner.Id, Name = "Original exercise" };
        var gym = new Gym { Id = Id<Gym>.New(), UserId = owner.Id, Name = "Original gym" };
        var training = new Training { Id = Id<Training>.New(), UserId = owner.Id, TypePlanDayId = day.Id, GymId = gym.Id };
        var score = new ExerciseScore { Id = Id<ExerciseScore>.New(), UserId = owner.Id, ExerciseId = exercise.Id, TrainingId = training.Id, Reps = 8, Series = 3 };
        db.UserRoles.Add(TestDataFactory.ApprovedTrainerRole(db, coach.Id));
        db.TrainerTraineeLinks.Add(new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = coach.Id, TraineeId = owner.Id });
        db.Plans.Add(plan);
        db.PlanDays.Add(day);
        db.Exercises.Add(exercise);
        db.Gyms.Add(gym);
        db.PlanDayExercises.Add(new PlanDayExercise { Id = Id<PlanDayExercise>.New(), PlanDayId = day.Id, ExerciseId = exercise.Id, Series = 3, Reps = "8" });
        db.Trainings.Add(training);
        db.ExerciseScores.Add(score);
        db.TrainingExerciseScores.Add(new TrainingExerciseScore { Id = Id<TrainingExerciseScore>.New(), TrainingId = training.Id, ExerciseScoreId = score.Id });
        await db.SaveChangesAsync();
        return new Graph(owner.Id, coach.Id, plan.Id, day.Id, exercise.Id, training.Id, score.Id);
    }

    private sealed record Graph(Id<User> OwnerId, Id<User> CoachId, Id<Plan> PlanId, Id<PlanDay> DayId,
        Id<Exercise> ExerciseId, Id<Training> TrainingId, Id<ExerciseScore> ScoreId);

    private sealed class SaveGate
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class GatedUnitOfWork(IUnitOfWork inner, SaveGate gate) : IUnitOfWork
    {
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            gate.Reached.TrySetResult();
            await gate.Release.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            return await inner.SaveChangesAsync(cancellationToken);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => inner.BeginTransactionAsync(cancellationToken);
    }
}
