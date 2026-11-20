using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Repositories.Reporting;
using Microsoft.EntityFrameworkCore;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ReportPhotoPersistenceRepositoryTests
{
    [Test]
    public async Task RecordAndFindUploadSession_PreservesMarkerAccountIds()
    {
        await using var db = CreateDbContext("record");
        var persistence = new ReportPhotoPersistenceRepository(db);
        var pending = CreatePendingUpload("photos/persisted.jpg", DateTimeOffset.UtcNow.AddMinutes(10));
        var photoTypeId = Id<TrainerPhotoType>.New();
        pending = pending with { SourcePhotoTypeId = photoTypeId, ReportPhotoSlotId = Id<PhotoUploadSession>.New().ToString() };

        await persistence.RecordUploadInitAsync(pending);
        await db.SaveChangesAsync();
        var reloaded = await persistence.FindUploadSessionAsync(pending.StorageKey);

        Assert.That(reloaded, Is.Not.Null);
        Assert.That(reloaded!.InitiatedByAccountId, Is.EqualTo(pending.InitiatedByAccountId));
        Assert.That(reloaded.OwnerAccountId, Is.EqualTo(pending.OwnerAccountId));
        Assert.That(reloaded.ReportRequestId, Is.EqualTo(pending.ReportRequestId));
        Assert.That(reloaded.SourcePhotoTypeId, Is.EqualTo(photoTypeId));
        Assert.That(reloaded.ReportPhotoSlotId, Is.EqualTo(pending.ReportPhotoSlotId));
    }

    [Test]
    public async Task MarkUploadCompleted_StagesCompletionAndClearsFailure()
    {
        await using var db = CreateDbContext("complete");
        var persistence = new ReportPhotoPersistenceRepository(db);
        var pending = CreatePendingUpload("photos/complete.jpg", DateTimeOffset.UtcNow.AddMinutes(10)) with
        {
            Status = PhotoUploadSessionStatus.Failed,
            FailureReason = "temporary"
        };
        await persistence.RecordUploadInitAsync(pending);
        await db.SaveChangesAsync();

        var photoId = Id<Photo>.New();
        var completedAt = DateTimeOffset.UtcNow;
        await persistence.MarkUploadCompletedAsync(pending.StorageKey, photoId, completedAt);

        var entity = await db.PhotoUploadSessions.SingleAsync();
        Assert.That(entity.Status, Is.EqualTo(PhotoUploadSessionStatus.Completed));
        Assert.That(entity.CompletedPhotoId, Is.EqualTo(photoId));
        Assert.That(entity.CompletedAtUtc, Is.EqualTo(completedAt));
        Assert.That(entity.FailureReason, Is.Null);
    }

    [TestCase(AccountDeletionState.Active)]
    [TestCase(AccountDeletionState.PendingDeletion)]
    public async Task CleanupCandidates_PreservesPendingAccountsAndOrdersActiveExpiredUploads(AccountDeletionState accountState)
    {
        await using var db = CreateDbContext("cleanup");
        var persistence = new ReportPhotoPersistenceRepository(db);
        var now = DateTimeOffset.UtcNow;
        await persistence.RecordUploadInitAsync(CreatePendingUpload("photos/pending.jpg", now.AddMinutes(-30)));
        await persistence.RecordUploadInitAsync(CreatePendingUpload("photos/failed.jpg", now.AddMinutes(-10)) with { Status = PhotoUploadSessionStatus.Failed });
        await persistence.RecordUploadInitAsync(CreatePendingUpload("photos/completed.jpg", now.AddMinutes(-20)) with { Status = PhotoUploadSessionStatus.Completed });
        await persistence.RecordUploadInitAsync(CreatePendingUpload("photos/future.jpg", now.AddMinutes(5)));
        var accounts = db.ChangeTracker.Entries<PhotoUploadSession>()
            .SelectMany(entry => new[] { entry.Entity.OwnerUserId, entry.Entity.InitiatedByUserId }).Distinct().ToArray();
        foreach (var id in accounts)
        {
            db.Users.Add(new User { Id = id, Name = id.ToString(), Email = $"{id}@example.test", DeletionState = accountState });
        }
        await db.SaveChangesAsync();

        var candidates = await persistence.ListCleanupCandidatesAsync(now);

        var expected = accountState == AccountDeletionState.Active
            ? new[] { "photos/pending.jpg", "photos/failed.jpg" } : [];
        Assert.That(candidates.Select(candidate => candidate.StorageKey), Is.EqualTo(expected));
    }

    [Test]
    public async Task SavePhoto_SoftDeletesExistingActivePhotoAndStagesReplacement()
    {
        await using var db = CreateDbContext("replace");
        var persistence = new ReportPhotoPersistenceRepository(db);
        var requestId = Id<ReportRequest>.New();
        var ownerId = Id<AccountReference>.New();
        var first = NewPhoto(requestId, ownerId, "photos/old.jpg");
        var slotId = Id<PhotoUploadSession>.New().ToString();
        var photoTypeId = Id<TrainerPhotoType>.New();
        first = first with { SourcePhotoTypeId = photoTypeId, ReportPhotoSlotId = slotId };
        var replacement = NewPhoto(requestId, ownerId, "photos/new.jpg") with { SourcePhotoTypeId = photoTypeId, ReportPhotoSlotId = slotId };
        await persistence.SaveAsync(first);
        await db.SaveChangesAsync();

        await persistence.SaveAsync(replacement);
        await db.SaveChangesAsync();

        var rows = await db.Photos.IgnoreQueryFilters().OrderBy(photo => photo.CreatedAt).ToListAsync();
        Assert.That(rows, Has.Exactly(2).Items);
        Assert.That(rows.Single(photo => photo.Id == first.Id).IsDeleted, Is.True);
        Assert.That(rows.Single(photo => photo.Id == replacement.Id).IsDeleted, Is.False);
        Assert.That(rows.Single(photo => photo.Id == replacement.Id).SourcePhotoTypeId, Is.EqualTo(photoTypeId));
        Assert.That(rows.Single(photo => photo.Id == replacement.Id).ReportPhotoSlotId, Is.EqualTo(slotId));
    }

    [Test]
    public async Task ListByRequests_ReturnsOnlyActivePhotosFromRequestedReports()
    {
        await using var db = CreateDbContext("requests");
        var persistence = new ReportPhotoPersistenceRepository(db);
        var ownerId = Id<AccountReference>.New();
        var firstRequestId = Id<ReportRequest>.New();
        var secondRequestId = Id<ReportRequest>.New();
        var excludedRequestId = Id<ReportRequest>.New();
        await persistence.SaveAsync(NewPhoto(firstRequestId, ownerId, "photos/first.jpg"));
        await persistence.SaveAsync(NewPhoto(secondRequestId, ownerId, "photos/second.jpg"));
        await persistence.SaveAsync(NewPhoto(excludedRequestId, ownerId, "photos/excluded.jpg"));
        await db.SaveChangesAsync();
        db.Photos.Single(photo => photo.ReportRequestId == secondRequestId).IsDeleted = true;
        await db.SaveChangesAsync();

        var photos = await persistence.ListByRequestsAsync([firstRequestId, secondRequestId]);

        Assert.That(photos.Select(photo => photo.StorageKey), Is.EqualTo(new[] { "photos/first.jpg" }));
    }

    private static AppDbContext CreateDbContext(string name)
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"report-photo-{name}-{Id<ReportPhotoPersistenceRepositoryTests>.New():N}")
            .Options);

    private static PendingPhotoUpload CreatePendingUpload(string storageKey, DateTimeOffset expiresAt)
        => new(
            Id<PhotoUploadSession>.New(),
            storageKey,
            Id<AccountReference>.New(),
            Id<AccountReference>.New(),
            Id<ReportRequest>.New(),
            PhotoViewType.Front.ToString(),
            "image/jpeg",
            1024,
            DateTimeOffset.UtcNow.AddMinutes(-30),
            expiresAt,
            null,
            null,
            PhotoUploadSessionStatus.Pending,
            null);

    private static NewReportPhotoPersistenceModel NewPhoto(
        Id<ReportRequest> requestId,
        Id<AccountReference> ownerId,
        string storageKey)
        => new(
            Id<Photo>.New(),
            storageKey,
            "image/jpeg",
            1024,
            "etag",
            null,
            PhotoViewType.Front.ToString(),
            requestId,
            ownerId,
            ownerId,
            DateTimeOffset.UtcNow);
}
