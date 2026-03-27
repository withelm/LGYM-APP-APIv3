using LgymApi.Domain.ValueObjects;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExerciseRepositoryTests
{
    [Test]
    public async Task GetTranslationsAsync_WithOnlyEmptyCultures_ReturnsEmptyDictionary()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"exercise-repo-{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new AppDbContext(options);

        var exerciseId = Guid.NewGuid();
        dbContext.Exercises.Add(new Exercise
        {
            Id = (LgymApi.Domain.ValueObjects.Id<Exercise>)exerciseId,
            Name = "Bench press"
        });
        dbContext.ExerciseTranslations.Add(new ExerciseTranslation
        {
            Id = (LgymApi.Domain.ValueObjects.Id<ExerciseTranslation>)Guid.NewGuid(),
            ExerciseId = (LgymApi.Domain.ValueObjects.Id<Exercise>)exerciseId,
            Culture = "en",
            Name = "Bench press"
        });
        await dbContext.SaveChangesAsync();

        var repository = new ExerciseRepository(dbContext);
        var result = await repository.GetTranslationsAsync([(LgymApi.Domain.ValueObjects.Id<Exercise>)exerciseId], ["   ", ""], CancellationToken.None);

        Assert.That(result, Is.Empty);
    }
}
