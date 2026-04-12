using FluentAssertions;
using LgymApi.Domain.ValueObjects;
using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

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
            Id = LgymApi.Domain.ValueObjects.Id<User>.New(),
            Name = "user",
            Email = "user@example.com",
            ProfileRank = "Rookie"
        };

        var plan = new Plan
        {
            Id = LgymApi.Domain.ValueObjects.Id<Plan>.New(),
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

        affectedRows.Should().Be(1);
        updatedPlan.IsActive.Should().BeFalse();
        (updatedPlan.UpdatedAt > previousUpdatedAt).Should().BeTrue();
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
            Id = LgymApi.Domain.ValueObjects.Id<User>.New(),
            Name = "user2",
            Email = "user2@example.com",
            ProfileRank = "Rookie"
        };

        var plan = new Plan
        {
            Id = LgymApi.Domain.ValueObjects.Id<Plan>.New(),
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

        affectedRows.Should().Be(1);
        updatedPlan.UpdatedAt.Should().Be(customUpdatedAt);
    }
}
