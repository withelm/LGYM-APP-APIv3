using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExecuteUpdateExtensionsTests
{
    [Test]
    public async Task StageUpdateAsync_WithSqlite_UpdatesSelectedPropertyAndUpdatedAt()
    {
        await using var sqliteConnection = new SqliteConnection("Data Source=:memory:");
        await sqliteConnection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(sqliteConnection)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "user",
            Email = "user@example.com",
            ProfileRank = "Rookie"
        };

        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = "Plan",
            IsActive = true
        };

        await dbContext.Users.AddAsync(user);
        await dbContext.Plans.AddAsync(plan);
        await dbContext.SaveChangesAsync();

        var previousUpdatedAt = plan.UpdatedAt;

        await Task.Delay(20);

        var affectedRows = await dbContext.Plans
            .Where(p => p.Id == plan.Id)
            .StageUpdateAsync(dbContext, p => p.IsActive, _ => false);

        var updatedPlan = await dbContext.Plans
            .AsNoTracking()
            .SingleAsync(p => p.Id == plan.Id);

        Assert.Multiple(() =>
        {
            Assert.That(affectedRows, Is.EqualTo(1));
            Assert.That(updatedPlan.IsActive, Is.False);
            Assert.That(updatedPlan.UpdatedAt, Is.GreaterThan(previousUpdatedAt));
        });
    }

    [Test]
    public async Task StageUpdateAsync_WithSqlite_UpdatingUpdatedAt_UsesProvidedValue()
    {
        await using var sqliteConnection = new SqliteConnection("Data Source=:memory:");
        await sqliteConnection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(sqliteConnection)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "user2",
            Email = "user2@example.com",
            ProfileRank = "Rookie"
        };

        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = "Plan 2",
            IsActive = true
        };

        await dbContext.Users.AddAsync(user);
        await dbContext.Plans.AddAsync(plan);
        await dbContext.SaveChangesAsync();

        var customUpdatedAt = new DateTimeOffset(2026, 2, 22, 12, 0, 0, TimeSpan.Zero);

        var affectedRows = await dbContext.Plans
            .Where(p => p.Id == plan.Id)
            .StageUpdateAsync(dbContext, p => p.UpdatedAt, _ => customUpdatedAt);

        var updatedPlan = await dbContext.Plans
            .AsNoTracking()
            .SingleAsync(p => p.Id == plan.Id);

        Assert.Multiple(() =>
        {
            Assert.That(affectedRows, Is.EqualTo(1));
            Assert.That(updatedPlan.UpdatedAt, Is.EqualTo(customUpdatedAt));
        });
    }
}
