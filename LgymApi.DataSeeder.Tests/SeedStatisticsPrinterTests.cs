using LgymApi.DataSeeder.Seeders;
using LgymApi.Domain.Entities;

namespace LgymApi.DataSeeder.Tests;

[TestFixture]
public sealed class SeedStatisticsPrinterTests
{
    [Test]
    public void PrintSummary_Should_Write_Seeded_Counts()
    {
        var context = new SeedContext
        {
            AdminUser = new User { Id = Guid.NewGuid(), Name = "Admin" },
            TesterUser = new User { Id = Guid.NewGuid(), Name = "Tester" }
        };

        context.DemoUsers.Add(new User { Id = Guid.NewGuid(), Name = "Demo" });
        context.Exercises.Add(new Exercise { Id = Guid.NewGuid(), Name = "Bench" });
        context.ExerciseTranslations.Add(new ExerciseTranslation { Id = Guid.NewGuid(), ExerciseId = context.Exercises[0].Id, Culture = "pl", Name = "Wyciskanie" });
        context.Addresses.Add(new Address { Id = Guid.NewGuid(), Name = "Main" });
        context.Gyms.Add(new Gym { Id = Guid.NewGuid(), Name = "Gym" });
        context.Plans.Add(new Plan { Id = Guid.NewGuid(), Name = "Plan" });
        context.PlanDays.Add(new PlanDay { Id = Guid.NewGuid(), Name = "Day" });
        context.PlanDayExercises.Add(new PlanDayExercise { Id = Guid.NewGuid(), Order = 1, Series = 3, Reps = "8-12" });
        context.Trainings.Add(new Training { Id = Guid.NewGuid() });
        context.ExerciseScores.Add(new ExerciseScore { Id = Guid.NewGuid() });
        context.TrainingExerciseScores.Add(new TrainingExerciseScore { Id = Guid.NewGuid() });
        context.Measurements.Add(new Measurement { Id = Guid.NewGuid(), Unit = "cm" });
        context.MainRecords.Add(new MainRecord { Id = Guid.NewGuid() });
        context.EloRegistries.Add(new EloRegistry { Id = Guid.NewGuid(), Elo = 1000 });
        context.AppConfigs.Add(new AppConfig { Id = Guid.NewGuid() });

        using var writer = new StringWriter();
        var original = Console.Out;
        Console.SetOut(writer);

        try
        {
            SeedStatisticsPrinter.PrintSummary(context);
        }
        finally
        {
            Console.SetOut(original);
        }

        var output = writer.ToString();
        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("Users: 3"));
            Assert.That(output, Does.Contain("Exercises: 1"));
            Assert.That(output, Does.Contain("Exercise translations: 1"));
            Assert.That(output, Does.Contain("Addresses: 1"));
            Assert.That(output, Does.Contain("Gyms: 1"));
            Assert.That(output, Does.Contain("Plans: 1"));
            Assert.That(output, Does.Contain("Plan days: 1"));
            Assert.That(output, Does.Contain("Plan day exercises: 1"));
            Assert.That(output, Does.Contain("Trainings: 1"));
            Assert.That(output, Does.Contain("Exercise scores: 1"));
            Assert.That(output, Does.Contain("Training exercise scores: 1"));
            Assert.That(output, Does.Contain("Measurements: 1"));
            Assert.That(output, Does.Contain("Main records: 1"));
            Assert.That(output, Does.Contain("Elo entries: 1"));
            Assert.That(output, Does.Contain("App configs: 1"));
        });
    }
}
