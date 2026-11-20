using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class UserRepositoryBatchReadTests
{
    [Test]
    public async Task GetByIdsAsync_ReturnsOnlyActiveMatchesWithoutTracking()
    {
        await using var dbContext = CreateDbContext();
        var activeUser = CreateUser();
        var deletedUser = CreateUser();
        deletedUser.IsDeleted = true;
        var missingUserId = Id<User>.New();
        dbContext.Users.AddRange(activeUser, deletedUser);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var repository = new UserRepository(dbContext, null!, null!);

        var users = await repository.GetByIdsAsync([deletedUser.Id, missingUserId, activeUser.Id]);

        Assert.That(users.Select(user => user.Id), Is.EqualTo(new[] { activeUser.Id }));
        Assert.That(dbContext.ChangeTracker.Entries<User>(), Is.Empty);
    }

    [Test]
    public async Task GetByIdsAsync_PropagatesCancellation()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        var repository = new UserRepository(dbContext, null!, null!);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = async () => await repository.GetByIdsAsync([user.Id], cancellationSource.Token);

        await Assert.ThatAsync(async () => { await action(); }, Throws.InstanceOf<OperationCanceledException>());
    }

    private static AppDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"user-repository-batch-{Id<UserRepositoryBatchReadTests>.New()}")
            .Options);

    private static User CreateUser() => new()
    {
        Id = Id<User>.New(),
        Name = $"user-{Id<User>.New().ToString().Replace("-", string.Empty, StringComparison.Ordinal)}",
        Email = $"user-{Id<User>.New().ToString().Replace("-", string.Empty, StringComparison.Ordinal)}@example.com"
    };
}
