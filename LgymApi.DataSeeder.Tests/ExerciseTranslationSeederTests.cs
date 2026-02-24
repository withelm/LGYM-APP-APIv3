using LgymApi.DataSeeder.Seeders;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.DataSeeder.Tests;

[TestFixture]
public sealed class ExerciseTranslationSeederTests
{
    [Test]
    public async Task SeedAsync_Should_Add_Polish_Translation_For_Bench_Press()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);

        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            Name = "Bench Press"
        };

        context.Exercises.Add(exercise);
        await context.SaveChangesAsync();

        var seedContext = new SeedContext();
        seedContext.Exercises.Add(exercise);

        var seeder = new ExerciseTranslationSeeder();

        await seeder.SeedAsync(context, seedContext, CancellationToken.None);
        await context.SaveChangesAsync();

        var translation = await context.ExerciseTranslations
            .SingleAsync(t => t.ExerciseId == exercise.Id && t.Culture == "pl");

        Assert.That(translation.Name, Is.EqualTo("Wyciskanie leżąc"));
    }
}
