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

    [Test]
    public async Task StageUpdateAsync_WithInMemory_FallsBackToClientEvaluation()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ef-inmem-fallback-{Id<ExecuteUpdateExtensionsTests>.New()}")
            .Options;

        await using var dbContext = new AppDbContext(options);
        var plan = new Plan
        {
            Id = Id<Plan>.New(),
            Name = "Plan",
            IsActive = true
        };
        await dbContext.Plans.AddAsync(plan);
        await dbContext.SaveChangesAsync();

        var affectedRows = await dbContext.Plans
            .Where(p => p.Id == plan.Id)
            .StageUpdateAsync(dbContext, p => p.IsActive, _ => false);

        affectedRows.Should().Be(1);
        plan.IsActive.Should().BeFalse();
    }

    [Test]
    public async Task StageUpdateAsync_WithSqlite_TwoProperties_UpdatesAndInjectsUpdatedAt()
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
            Id = Id<User>.New(),
            Name = "user",
            Email = "user@example.com",
            ProfileRank = "Rookie"
        };

        var plan = new Plan
        {
            Id = Id<Plan>.New(),
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
            .StageUpdateAsync(
                dbContext,
                p => p.IsActive,
                _ => false,
                p => p.Name,
                _ => "Updated");

        var updatedPlan = await dbContext.Plans
            .AsNoTracking()
            .SingleAsync(p => p.Id == plan.Id);

        affectedRows.Should().Be(1);
        updatedPlan.IsActive.Should().BeFalse();
        updatedPlan.Name.Should().Be("Updated");
        (updatedPlan.UpdatedAt > previousUpdatedAt).Should().BeTrue();
    }

    [Test]
    public async Task StageUpdateAsync_TwoProperties_WithInMemory_UpdatesBothViaFallback()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ef-inmem-fallback2-{Id<ExecuteUpdateExtensionsTests>.New()}")
            .Options;

        await using var dbContext = new AppDbContext(options);
        var plan = new Plan
        {
            Id = Id<Plan>.New(),
            Name = "Plan",
            IsActive = true
        };
        await dbContext.Plans.AddAsync(plan);
        await dbContext.SaveChangesAsync();

        var affectedRows = await dbContext.Plans
            .Where(p => p.Id == plan.Id)
            .StageUpdateAsync(
                dbContext,
                p => p.IsActive,
                _ => false,
                p => p.Name,
                _ => "Updated");

        affectedRows.Should().Be(1);
        plan.IsActive.Should().BeFalse();
        plan.Name.Should().Be("Updated");
    }

    [Test]
    public async Task StageUpdateAsync_NoDbContext_OnNonInfrastructureSource_ThrowsInvalidOperationException()
    {
        IQueryable<Plan> source = new List<Plan>().AsQueryable();

        var act = async () => await source.StageUpdateAsync(p => p.IsActive, _ => false);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*DbContext is required*");
    }

    [Test]
    public async Task StageUpdateAsync_WithNonPropertySelector_ThrowsInvalidOperationException()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ef-inmem-prop-{Id<ExecuteUpdateExtensionsTests>.New()}")
            .Options;

        await using var dbContext = new AppDbContext(options);
        var plan = new Plan
        {
            Id = Id<Plan>.New(),
            Name = "Plan",
            IsActive = true
        };
        await dbContext.Plans.AddAsync(plan);
        await dbContext.SaveChangesAsync();

        var act = async () => await dbContext.Plans
            .Where(p => p.Id == plan.Id)
            .StageUpdateAsync(dbContext, p => p.Id.GetHashCode(), p => 0);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Property selector must target a property*");
    }

    [Test]
    public async Task StageUpdateAsync_WithNonEntityOnSqlite_DoesNotInjectUpdatedAt()
    {
        await using var sqliteConnection = new SqliteConnection("Data Source=:memory:");
        await sqliteConnection.OpenAsync();

        var options = new DbContextOptionsBuilder<NonEntityContext>()
            .UseSqlite(sqliteConnection)
            .Options;

        await using var ctx = new NonEntityContext(options);
        await ctx.Database.EnsureCreatedAsync();

        var item = new NonEntity { Id = 1, Name = "original" };
        await ctx.Items.AddAsync(item);
        await ctx.SaveChangesAsync();

        var affectedRows = await ctx.Items
            .Where(x => x.Id == 1)
            .StageUpdateAsync(ctx, x => x.Name, _ => "changed");

        affectedRows.Should().Be(1);

        var loaded = await ctx.Items.AsNoTracking().SingleAsync(x => x.Id == 1);
        loaded.Name.Should().Be("changed");
    }

#pragma warning disable CS0618
    [Test]
    public async Task ExecuteUpdateAsync_Obsolete_WithDbContext_UsesFallbackOnInMemory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ef-inmem-obs1-{Id<ExecuteUpdateExtensionsTests>.New()}")
            .Options;

        await using var dbContext = new AppDbContext(options);
        var plan = new Plan
        {
            Id = Id<Plan>.New(),
            Name = "Plan",
            IsActive = true
        };
        await dbContext.Plans.AddAsync(plan);
        await dbContext.SaveChangesAsync();

        var affectedRows = await dbContext.Plans
            .Where(p => p.Id == plan.Id)
            .ExecuteUpdateAsync(dbContext, p => p.IsActive, _ => false);

        affectedRows.Should().Be(1);
        plan.IsActive.Should().BeFalse();
    }

#pragma warning restore CS0618

    private sealed class NonEntityContext : DbContext
    {
        public NonEntityContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<NonEntity> Items => Set<NonEntity>();
    }

    private sealed class NonEntity
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
