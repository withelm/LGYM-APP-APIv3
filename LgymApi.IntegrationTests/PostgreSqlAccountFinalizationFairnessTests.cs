using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.AccountDeletion.Finalization;
using LgymApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace LgymApi.IntegrationTests;

[TestFixture]
[Category("PostgreSql")]
internal sealed class PostgreSqlAccountFinalizationFairnessTests : PostgreSqlIntegrationTestBase
{
    [Test]
    public async Task ProcessDueAsync_DeferredPageDoesNotStarveIndependentAccount()
    {
        var now = DateTimeOffset.UtcNow;
        var blockedIds = Enumerable.Range(1, 100)
            .Select(i =>
            {
                Assert.That(Id<User>.TryParse($"00000000-0000-0000-0000-{i:x12}", out var id), Is.True);
                return id;
            }).ToArray();
        Assert.That(Id<User>.TryParse("ffffffff-ffff-ffff-ffff-ffffffffffff", out var laterId), Is.True);
        var participantId = Id<User>.New();

        await using (var setup = Factory.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            var participant = new User { Id = participantId, Name = "recoverable", Email = new Email("recoverable@example.test"),
                DeletionState = AccountDeletionState.PendingDeletion, DeletionEffectiveAt = now.AddDays(1), DeletionGeneration = Id<CorrelationScope>.New() };
            db.Users.Add(participant);
            foreach (var id in blockedIds.Append(laterId))
            {
                var generation = Id<CorrelationScope>.New();
                db.Users.Add(new User { Id = id, Name = $"user-{id}", Email = new Email($"{id}@example.test"),
                    DeletionState = AccountDeletionState.PendingDeletion, DeletionEffectiveAt = now.AddDays(id == laterId ? -1 : -2),
                    DeletionGeneration = generation, DeletionScheduledGeneration = generation });
                if (id != laterId)
                    db.TraineeNotes.Add(new TraineeNote { Id = Id<TraineeNote>.New(), TrainerId = id, TraineeId = participantId, LastUpdatedByUserId = id, Content = "retained" });
            }
            await db.SaveChangesAsync();
        }

        await using (var execution = Factory.Services.CreateAsyncScope())
            await execution.ServiceProvider.GetRequiredService<IAccountFinalizationService>().ProcessDueAsync();

        await using var verify = Factory.Services.CreateAsyncScope();
        var database = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.That((await database.Users.IgnoreQueryFilters().CountAsync(user => blockedIds.Contains(user.Id) && user.DeletionState == AccountDeletionState.PendingDeletion)), Is.EqualTo(100));
        Assert.That((await database.TraineeNotes.IgnoreQueryFilters().CountAsync(note => note.TraineeId == participantId)), Is.EqualTo(100));
        Assert.That((await database.Users.IgnoreQueryFilters().SingleAsync(user => user.Id == laterId)).DeletionState, Is.EqualTo(AccountDeletionState.PermanentlyDeleted));
    }
}
