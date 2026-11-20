using LgymApi.Application.Abstractions.Storage;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories.Reporting;
using LgymApi.Infrastructure.Services;
using LgymApi.Infrastructure.UnitOfWork;
using LgymApi.Reporting.AccountDeletion;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace LgymApi.Reporting.UnitTests;

[TestFixture]
public sealed class AccountPhotoDeletionTests
{
    [Test]
    public async Task PendingAccount_CannotStageIrreversiblePhotoDeletion()
    {
        await using var database = CreateDatabase();
        var user = new User { Id = Id<User>.New(), DeletionState = AccountDeletionState.PendingDeletion, DeletionGeneration = Id<CorrelationScope>.New() };
        database.Users.Add(user);
        await database.SaveChangesAsync();
        var persistence = new PhotoDeletionPersistence(database);

        var stage = () => persistence.StageAccountAsync(user.Id.Rebind<AccountReference>(), user.DeletionGeneration.Value, default);

        await Assert.ThatAsync(async () => { await stage(); }, Throws.InstanceOf<InvalidOperationException>());
        Assert.That((await database.PhotoDeletionWorkItems.CountAsync()), Is.EqualTo(0));
    }

    [Test]
    public async Task CommittedManifest_DeletesOriginalThumbnailReplacedUploadAndUnreferencedObjects()
    {
        await using var database = CreateDatabase();
        var generation = Id<CorrelationScope>.New();
        var user = new User { Id = Id<User>.New(), DeletionState = AccountDeletionState.Finalizing, DeletionGeneration = generation };
        var prefix = $"photos/{user.Id}/";
        var keys = new[] { prefix + "original.jpg", prefix + "thumbnail.jpg", prefix + "replaced.jpg", prefix + "upload.jpg", prefix + "orphan.jpg" };
        var store = new LocalPhotoDevelopmentStore();
        var provider = new LocalPhotoStorageProvider(new HttpContextAccessor(), store, new LocalPhotoDevelopmentUrlSigner(new string('k', 32), TimeProvider.System));
        database.Users.Add(user);
        database.Photos.AddRange(
            new Photo { Id = Id<Photo>.New(), OwnerUserId = user.Id, StorageKey = keys[0], ThumbnailStorageKey = keys[1] },
            new Photo { Id = Id<Photo>.New(), OwnerUserId = user.Id, StorageKey = keys[2], IsDeleted = true });
        database.PhotoUploadSessions.Add(new PhotoUploadSession
        {
            Id = Id<PhotoUploadSession>.New(), OwnerUserId = user.Id, StorageKey = keys[3],
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-30), IsDeleted = true
        });
        await database.SaveChangesAsync();
        foreach (var key in keys) await store.SaveAsync(key, new MemoryStream([1, 2, 3]));
        var service = new AccountPhotoDeletionService(new PhotoDeletionPersistence(database), provider, provider, new EfUnitOfWork(database), TimeProvider.System);
        try
        {
            await service.StageAsync(user.Id.Rebind<AccountReference>(), generation);
            database.Photos.RemoveRange(database.Photos.IgnoreQueryFilters());
            database.PhotoUploadSessions.RemoveRange(database.PhotoUploadSessions.IgnoreQueryFilters());
            await database.SaveChangesAsync();

            await service.ProcessPendingAsync();
            await service.ProcessPendingAsync();

            foreach (var key in keys) Assert.That((await store.ReadAsync(key)), Is.Null);
            Assert.That((await database.PhotoDeletionWorkItems.CountAsync(item => item.CompletedAtUtc == null)), Is.EqualTo(0));
        }
        finally
        {
            foreach (var key in keys) await store.DeleteAsync(key);
        }
    }

    [Test]
    public async Task ProviderFailure_DefersOnlyFailedWorkAndFreshProcessorRetriesIt()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Id<CorrelationScope>.New().ToString()).Options;
        var itemId = Id<PhotoDeletionWorkItem>.New();
        var successfulId = Id<PhotoDeletionWorkItem>.New();
        await using (var database = new AppDbContext(options))
        {
            database.PhotoDeletionWorkItems.Add(new PhotoDeletionWorkItem
            {
                Id = itemId, DeletionGeneration = Id<CorrelationScope>.New(), StorageKey = "photos/retry.jpg"
            });
            database.PhotoDeletionWorkItems.Add(new PhotoDeletionWorkItem
            {
                Id = successfulId, DeletionGeneration = Id<CorrelationScope>.New(), StorageKey = "photos/success.jpg"
            });
            await database.SaveChangesAsync();
            var failingStorage = Substitute.For<IPhotoStorageProvider>();
            failingStorage.DeleteAsync("photos/retry.jpg", Arg.Any<CancellationToken>()).Returns(Task.FromException(new IOException("Unavailable")));
            var service = new AccountPhotoDeletionService(new PhotoDeletionPersistence(database), failingStorage,
                Substitute.For<IPhotoStorageInventory>(), new EfUnitOfWork(database), TimeProvider.System);

            await Assert.ThatAsync(async () => { await (((Func<Task>)(() => service.ProcessPendingAsync())))(); }, Throws.InstanceOf<InvalidOperationException>());
        }

        await using var retryDatabase = new AppDbContext(options);
        Assert.That((await retryDatabase.PhotoDeletionWorkItems.SingleAsync(item => item.Id == itemId)).CompletedAtUtc, Is.Null);
        Assert.That((await retryDatabase.PhotoDeletionWorkItems.SingleAsync(item => item.Id == successfulId)).CompletedAtUtc, Is.Not.Null);
        var storage = Substitute.For<IPhotoStorageProvider>();
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(DateTimeOffset.UtcNow.AddMinutes(6));
        var retry = new AccountPhotoDeletionService(new PhotoDeletionPersistence(retryDatabase), storage,
            Substitute.For<IPhotoStorageInventory>(), new EfUnitOfWork(retryDatabase), clock);
        await retry.ProcessPendingAsync();
        Assert.That((await retryDatabase.PhotoDeletionWorkItems.SingleAsync(item => item.Id == itemId)).CompletedAtUtc, Is.Not.Null);
        await storage.Received(1).DeleteAsync("photos/retry.jpg", Arg.Any<CancellationToken>());
    }

    private static AppDbContext CreateDatabase()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Id<CorrelationScope>.New().ToString()).Options);
}
