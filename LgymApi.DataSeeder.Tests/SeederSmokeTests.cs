using LgymApi.DataSeeder.Seeders;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Tests;

[TestFixture]
public sealed class SeederSmokeTests
{
    [Test]
    public async Task ExerciseSeeder_Should_Add_Default_Exercises()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext();

        var seeder = new ExerciseSeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(await context.Exercises.CountAsync(), Is.GreaterThanOrEqualTo(6));
    }

    [Test]
    public async Task AddressSeeder_Should_Add_Default_Addresses()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext();

        var seeder = new AddressSeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(await context.Addresses.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task GymSeeder_Should_Add_Gyms_For_Demo_Users()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext();

        seedContext.DemoUsers.Add(new User { Id = (Id<User>)Guid.NewGuid(), Name = "Demo1" });
        seedContext.DemoUsers.Add(new User { Id = (Id<User>)Guid.NewGuid(), Name = "Demo2" });

        var addressSeeder = new AddressSeeder();
        await addressSeeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        var seeder = new GymSeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(await context.Gyms.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task PlanSeeder_Should_Add_Push_Pull_Legs_Plan()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext();

        var demoUser = new User { Id = (Id<User>)Guid.NewGuid(), Name = "Demo" };
        seedContext.DemoUsers.Add(demoUser);

        var seeder = new PlanSeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        var plan = await context.Plans.SingleAsync();
        Assert.That(plan.Name, Is.EqualTo("Push Pull Legs"));
        Assert.That(plan.UserId, Is.EqualTo(demoUser.Id));
    }

    [Test]
    public async Task PlanDaySeeder_Should_Add_Three_Days_Per_Plan()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext();

        var plan = new Plan { Id = (Id<Plan>)Guid.NewGuid(), Name = "PPL", UserId = (Id<User>)Guid.NewGuid() };
        seedContext.Plans.Add(plan);

        var seeder = new PlanDaySeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        var names = await context.PlanDays.Select(day => day.Name).ToListAsync();
        Assert.That(names, Does.Contain("Push"));
        Assert.That(names, Does.Contain("Pull"));
        Assert.That(names, Does.Contain("Legs"));
    }

    [Test]
    public async Task PlanDayExerciseSeeder_Should_Add_Exercises_To_Days()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext();

        var planDay = new PlanDay { Id = (Id<PlanDay>)Guid.NewGuid(), Name = "Push", PlanId = (Id<Plan>)Guid.NewGuid() };
        seedContext.PlanDays.Add(planDay);

        var exerciseSeeder = new ExerciseSeeder();
        await exerciseSeeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        var seeder = new PlanDayExerciseSeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(await context.PlanDayExercises.CountAsync(), Is.GreaterThan(0));
    }

    [Test]
    public async Task TrainingSeeder_Should_Add_Trainings_For_Demo_Users()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext();

        var user = new User { Id = (Id<User>)Guid.NewGuid(), Name = "Demo" };
        seedContext.DemoUsers.Add(user);

        var planDay = new PlanDay { Id = (Id<PlanDay>)Guid.NewGuid(), Name = "Push", PlanId = (Id<Plan>)Guid.NewGuid() };
        seedContext.PlanDays.Add(planDay);

        var addressSeeder = new AddressSeeder();
        await addressSeeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        var gymSeeder = new GymSeeder();
        await gymSeeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        var seeder = new TrainingSeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(await context.Trainings.CountAsync(), Is.GreaterThan(0));
    }

    [Test]
    public async Task ExerciseScoreSeeder_Should_Add_Scores_For_Trainings()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext();

        var user = new User { Id = (Id<User>)Guid.NewGuid(), Name = "Demo" };
        seedContext.DemoUsers.Add(user);

        var exerciseSeeder = new ExerciseSeeder();
        await exerciseSeeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        var training = new Training { Id = (Id<Training>)Guid.NewGuid(), UserId = user.Id, TypePlanDayId = (Id<PlanDay>)Guid.NewGuid(), GymId = (Id<Gym>)Guid.NewGuid() };
        seedContext.Trainings.Add(training);
        context.Trainings.Add(training);
        await context.SaveChangesAsync();

        var seeder = new ExerciseScoreSeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(await context.ExerciseScores.CountAsync(), Is.GreaterThan(0));
    }

    [Test]
    public async Task TrainingExerciseScoreSeeder_Should_Link_Scores_To_Trainings()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext();

        var training = new Training { Id = (Id<Training>)Guid.NewGuid(), UserId = (Id<User>)Guid.NewGuid(), TypePlanDayId = (Id<PlanDay>)Guid.NewGuid(), GymId = (Id<Gym>)Guid.NewGuid() };
        var exerciseScore = new ExerciseScore { Id = (Id<ExerciseScore>)Guid.NewGuid(), ExerciseId = (Id<Exercise>)Guid.NewGuid(), UserId = training.UserId, TrainingId = training.Id, Series = 3, Reps = 8, Weight = 50, Unit = WeightUnits.Kilograms };

        seedContext.Trainings.Add(training);
        seedContext.ExerciseScores.Add(exerciseScore);

        context.Trainings.Add(training);
        context.ExerciseScores.Add(exerciseScore);
        await context.SaveChangesAsync();

        var seeder = new TrainingExerciseScoreSeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(await context.TrainingExerciseScores.CountAsync(), Is.GreaterThan(0));
    }

     [Test]
     public async Task MeasurementSeeder_Should_Add_Measurements_For_Demo_Users()
     {
         var context = await CreateContextAsync();
         var seedContext = new SeedContext();

         var user = new User { Id = (Id<User>)Guid.NewGuid(), Name = "Demo" };
         seedContext.DemoUsers.Add(user);

        var seeder = new MeasurementSeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(await context.Measurements.CountAsync(), Is.GreaterThan(0));
    }

     [Test]
     public async Task MainRecordSeeder_Should_Add_Records_For_Demo_Users()
     {
         var context = await CreateContextAsync();
         var seedContext = new SeedContext();

         var user = new User { Id = (Id<User>)Guid.NewGuid(), Name = "Demo" };
         seedContext.DemoUsers.Add(user);

        var exerciseSeeder = new ExerciseSeeder();
        await exerciseSeeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        var seeder = new MainRecordSeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(await context.MainRecords.CountAsync(), Is.GreaterThan(0));
    }

    [Test]
    public async Task EloRegistrySeeder_Should_Add_Initial_Entries()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext
        {
            AdminUser = new User { Id = (Id<User>)Guid.NewGuid(), Name = "Admin" }
        };

        var seeder = new EloRegistrySeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(await context.EloRegistries.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task AppConfigSeeder_Should_Add_Default_Configs()
    {
        var context = await CreateContextAsync();
        var seedContext = new SeedContext();

        var seeder = new AppConfigSeeder();
        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.That(await context.AppConfigs.CountAsync(), Is.EqualTo(2));
    }

    private static async Task<AppDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }
}
