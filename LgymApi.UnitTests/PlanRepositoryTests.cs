using LgymApi.Domain.Entities;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class PlanRepositoryTests
{
    private const int ExpectedShareCodeGenerationAttempts = 10;

    [Test]
    public async Task GenerateShareCodeAsync_WhenPlanAlreadyHasUniqueCode_ReturnsExistingCode()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"plan-repo-existing-{Guid.NewGuid()}")
            .Options;

        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        await using var dbContext = new AppDbContext(options);
        dbContext.Plans.Add(new Plan
        {
            Id = planId,
            UserId = userId,
            Name = "Plan",
            ShareCode = "EXISTING01"
        });
        await dbContext.SaveChangesAsync();

        var repository = new PlanRepository(dbContext, _ => "NEVERUSED1");

        var result = await repository.GenerateShareCodeAsync(planId, userId, CancellationToken.None);

        Assert.That(result, Is.EqualTo("EXISTING01"));
    }

    [Test]
    public async Task GenerateShareCodeAsync_WhenGeneratedCodeCollides_RetriesUntilUnique()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"plan-repo-collision-{Guid.NewGuid()}")
            .Options;

        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        await using var dbContext = new AppDbContext(options);
        dbContext.Plans.AddRange(
            new Plan
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Owner Plan",
                ShareCode = "COLLIDE001"
            },
            new Plan
            {
                Id = planId,
                UserId = userId,
                Name = "Target Plan",
                ShareCode = null
            });
        await dbContext.SaveChangesAsync();

        var generatedCodes = new Queue<string>(["COLLIDE001", "UNIQUE0001"]);
        var repository = new PlanRepository(dbContext, _ => generatedCodes.Dequeue());

        var result = await repository.GenerateShareCodeAsync(planId, userId, CancellationToken.None);

        Assert.That(result, Is.EqualTo("UNIQUE0001"));
    }

    [Test]
    public async Task GenerateShareCodeAsync_WhenExistingCodeIsTaken_RegeneratesUniqueCode()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"plan-repo-regenerate-existing-{Guid.NewGuid()}")
            .Options;

        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        await using var dbContext = new AppDbContext(options);
        dbContext.Plans.AddRange(
            new Plan
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Existing Plan",
                ShareCode = "DUPLICATE01"
            },
            new Plan
            {
                Id = planId,
                UserId = userId,
                Name = "Target Plan",
                ShareCode = "DUPLICATE01"
            });
        await dbContext.SaveChangesAsync();

        var repository = new PlanRepository(dbContext, _ => "UNIQUE0001");

        var result = await repository.GenerateShareCodeAsync(planId, userId, CancellationToken.None);

        Assert.That(result, Is.EqualTo("UNIQUE0001"));
    }

    [Test]
    public async Task GenerateShareCodeAsync_WhenGeneratorReturnsInvalidCode_SkipsAndGeneratesValidCode()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"plan-repo-invalid-generator-{Guid.NewGuid()}")
            .Options;

        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        await using var dbContext = new AppDbContext(options);
        dbContext.Plans.Add(new Plan
        {
            Id = planId,
            UserId = userId,
            Name = "Target Plan"
        });
        await dbContext.SaveChangesAsync();

        var generatedCodes = new Queue<string>(["bad", "UNIQUE0001"]);
        var repository = new PlanRepository(dbContext, _ => generatedCodes.Dequeue());

        var result = await repository.GenerateShareCodeAsync(planId, userId, CancellationToken.None);

        Assert.That(result, Is.EqualTo("UNIQUE0001"));
    }

    [Test]
    public async Task GenerateShareCodeAsync_WhenAllAttemptsCollide_ThrowsInvalidOperationException()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"plan-repo-max-attempts-{Guid.NewGuid()}")
            .Options;

        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var generationCount = 0;

        await using var dbContext = new AppDbContext(options);
        dbContext.Plans.AddRange(
            new Plan
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Existing Plan",
                ShareCode = "COLLIDE001"
            },
            new Plan
            {
                Id = planId,
                UserId = userId,
                Name = "Target Plan"
            });
        await dbContext.SaveChangesAsync();

        var repository = new PlanRepository(
            dbContext,
            _ =>
            {
                generationCount++;
                return "COLLIDE001";
            });

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await repository.GenerateShareCodeAsync(planId, userId, CancellationToken.None));

        Assert.That(exception!.Message, Is.EqualTo("Unable to generate unique share code"));
        Assert.That(generationCount, Is.EqualTo(ExpectedShareCodeGenerationAttempts));
    }
}
