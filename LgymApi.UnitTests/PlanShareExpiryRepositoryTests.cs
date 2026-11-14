using FluentAssertions;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class PlanShareExpiryRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestCase(1)]
    [TestCase(7)]
    [TestCase(30)]
    public async Task Issue_SetsRequestedPeriodAndExpiry(int days)
    {
        await using var db = CreateContext();
        var plan = new Plan { Id = Id<Plan>.New(), UserId = Id<User>.New(), Name = "Plan" };
        db.Plans.Add(plan);
        await db.SaveChangesAsync();
        var repository = new PlanRepository(db, _ => "NEWCODE001");

        var code = await repository.GenerateShareCodeAsync(plan.Id, plan.UserId, days, Now);

        code.Should().Be("NEWCODE001");
        plan.ShareCodeDurationDays.Should().Be(days);
        plan.ShareCodeExpiresAt.Should().Be(Now.AddDays(days));
    }

    [TestCase(7, 1, false)]
    [TestCase(1, 1, true)]
    [TestCase(7, 0, true)]
    [TestCase(7, -1, true)]
    public async Task Issue_OnlyReusesUnexpiredCodeForSamePeriod(int existingDays, int remainingDays, bool rotates)
    {
        await using var db = CreateContext();
        var plan = new Plan
        {
            Id = Id<Plan>.New(), UserId = Id<User>.New(), Name = "Plan", ShareCode = "OLDCODE001",
            ShareCodeDurationDays = existingDays, ShareCodeExpiresAt = Now.AddDays(remainingDays)
        };
        db.Plans.Add(plan);
        await db.SaveChangesAsync();
        var repository = new PlanRepository(db, _ => "NEWCODE001");

        var code = await repository.GenerateShareCodeAsync(plan.Id, plan.UserId, 7, Now);

        code.Should().Be(rotates ? "NEWCODE001" : "OLDCODE001");
        plan.ShareCodeExpiresAt.Should().Be(rotates ? Now.AddDays(7) : Now.AddDays(remainingDays));
        plan.ShareCodeDurationDays.Should().Be(7);
    }

    [Test]
    public async Task Issue_CollisionExhaustion_DoesNotModifyExistingCode()
    {
        await using var db = CreateContext();
        var owner = Id<User>.New();
        var plan = new Plan { Id = Id<Plan>.New(), UserId = owner, Name = "Plan", ShareCode = "OLDCODE001" };
        db.Plans.AddRange(plan, new Plan { Id = Id<Plan>.New(), UserId = owner, Name = "Other", ShareCode = "COLLIDE001" });
        await db.SaveChangesAsync();
        var repository = new PlanRepository(db, _ => "COLLIDE001");

        var action = () => repository.GenerateShareCodeAsync(plan.Id, owner, 7, Now);

        await action.Should().ThrowAsync<InvalidOperationException>();
        plan.ShareCode.Should().Be("OLDCODE001");
        plan.ShareCodeExpiresAt.Should().BeNull();
        plan.ShareCodeDurationDays.Should().BeNull();
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"share-expiry-{Id<PlanShareExpiryRepositoryTests>.New()}").Options);
}
