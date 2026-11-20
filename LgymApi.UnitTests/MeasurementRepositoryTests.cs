using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class MeasurementRepositoryTests
{
    [Test]
    public async Task AddAsync_AndFindByIdAsync_PersistMeasurement()
    {
        await using var db = CreateDbContext("measurement-repo-add");
        var repository = new MeasurementRepository(db);
        var measurement = CreateMeasurement(Id<User>.New(), BodyParts.Chest, DateTimeOffset.UtcNow);

        await repository.AddAsync(measurement);
        await db.SaveChangesAsync();

        var found = await repository.FindByIdAsync(measurement.Id);

        Assert.That(found, Is.Not.Null);
        Assert.That(found!.Id, Is.EqualTo(measurement.Id));
    }

    [Test]
    public async Task GetByUserAsync_WhenBodyPartProvided_FiltersResults()
    {
        await using var db = CreateDbContext("measurement-repo-user");
        var userId = Id<User>.New();
        db.Measurements.AddRange(
            CreateMeasurement(userId, BodyParts.Chest, DateTimeOffset.UtcNow),
            CreateMeasurement(userId, BodyParts.Waist, DateTimeOffset.UtcNow),
            CreateMeasurement(Id<User>.New(), BodyParts.Chest, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        var repository = new MeasurementRepository(db);

        var all = await repository.GetByUserAsync(userId, null);
        var chestOnly = await repository.GetByUserAsync(userId, BodyParts.Chest);

        Assert.That(all, Has.Exactly(2).Items);
        var assertedValue130 = (chestOnly).ToArray();
        Assert.That(assertedValue130, Has.Exactly(1).Items);
        Assert.That(assertedValue130.Single().BodyPart, Is.EqualTo(BodyParts.Chest));
    }

    [Test]
    public async Task GetExistingBodyPartsByUserAndCreatedAtRangeAsync_WhenBodyPartsEmpty_ReturnsEmptySet()
    {
        await using var db = CreateDbContext("measurement-repo-empty-range");
        var repository = new MeasurementRepository(db);

        var existing = await repository.GetExistingBodyPartsByUserAndCreatedAtRangeAsync(Id<User>.New(), [], DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);

        Assert.That(existing, Is.Empty);
    }

    [Test]
    public async Task GetExistingBodyPartsByUserAndCreatedAtRangeAsync_ReturnsDistinctBodyPartsWithinHalfOpenWindow()
    {
        await using var db = CreateDbContext("measurement-repo-range");
        var repository = new MeasurementRepository(db);
        var userId = Id<User>.New();
        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to = DateTimeOffset.UtcNow;
        db.Measurements.AddRange(
            CreateMeasurement(userId, BodyParts.Chest, from),
            CreateMeasurement(userId, BodyParts.Chest, from.AddDays(1)),
            CreateMeasurement(userId, BodyParts.Waist, to.AddMinutes(-1)),
            CreateMeasurement(userId, BodyParts.Biceps, to),
            CreateMeasurement(Id<User>.New(), BodyParts.Chest, from.AddDays(1)));
        await db.SaveChangesAsync();

        var existing = await repository.GetExistingBodyPartsByUserAndCreatedAtRangeAsync(userId, [BodyParts.Chest, BodyParts.Waist, BodyParts.Biceps], from, to);

        Assert.That(existing, Is.EquivalentTo(new[] { BodyParts.Chest, BodyParts.Waist }));
    }

    private static AppDbContext CreateDbContext(string name)
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"{name}-{Id<MeasurementRepositoryTests>.New()}")
            .Options);

    private static Measurement CreateMeasurement(Id<User> userId, BodyParts bodyPart, DateTimeOffset createdAt)
        => new()
        {
            Id = Id<Measurement>.New(),
            UserId = userId,
            BodyPart = bodyPart,
            Unit = "cm",
            Value = 10,
            CreatedAt = createdAt
        };
}
