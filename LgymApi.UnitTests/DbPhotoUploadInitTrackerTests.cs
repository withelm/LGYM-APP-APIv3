using FluentAssertions;
using LgymApi.Application.Features.Reporting;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Data;
using LgymApi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class DbPhotoUploadInitTrackerTests
{
    [Test]
    public async Task RecordUploadInitAsync_ThenGetUploadSessionAsync_PersistsMappedSession()
    {
        await using var db = CreateDbContext("photo-upload-tracker-record");
        var tracker = new DbPhotoUploadInitTracker(db);
        var pendingUpload = CreatePendingUpload("photos/persisted.jpg", DateTimeOffset.UtcNow.AddMinutes(10));
        pendingUpload.Id = default;

        await tracker.RecordUploadInitAsync(pendingUpload);
        await db.SaveChangesAsync();

        var storedEntity = await db.PhotoUploadSessions.SingleAsync();
        var reloaded = await tracker.GetUploadSessionAsync(pendingUpload.StorageKey);

        storedEntity.Id.Should().NotBe(default(Id<PhotoUploadSession>));
        storedEntity.StorageKey.Should().Be(pendingUpload.StorageKey);
        reloaded.Should().NotBeNull();
        reloaded!.ReportRequestId.Should().Be(pendingUpload.ReportRequestId);
        reloaded.Status.Should().Be(PhotoUploadSessionStatus.Pending);
    }

    [Test]
    public async Task MarkCompletedAsync_UpdatesCompletionFieldsAndClearsFailureReason()
    {
        await using var db = CreateDbContext("photo-upload-tracker-complete");
        var session = CreateEntity("photos/complete.jpg", PhotoUploadSessionStatus.Failed, DateTimeOffset.UtcNow.AddMinutes(10));
        session.FailureReason = "temporary failure";
        db.PhotoUploadSessions.Add(session);
        await db.SaveChangesAsync();

        var tracker = new DbPhotoUploadInitTracker(db);
        var photoId = Id<Photo>.New();
        var completedAt = DateTimeOffset.UtcNow;

        await tracker.MarkCompletedAsync(session.StorageKey, photoId, completedAt);

        session.Status.Should().Be(PhotoUploadSessionStatus.Completed);
        session.CompletedPhotoId.Should().Be(photoId);
        session.CompletedAtUtc.Should().Be(completedAt);
        session.FailureReason.Should().BeNull();
        session.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task MarkFailedAsync_AndMarkExpiredAsync_UpdateStatusWhenSessionExists()
    {
        await using var db = CreateDbContext("photo-upload-tracker-status");
        var session = CreateEntity("photos/status.jpg", PhotoUploadSessionStatus.Pending, DateTimeOffset.UtcNow.AddMinutes(10));
        db.PhotoUploadSessions.Add(session);
        await db.SaveChangesAsync();

        var tracker = new DbPhotoUploadInitTracker(db);

        await tracker.MarkFailedAsync(session.StorageKey, "upload verification failed");
        session.Status.Should().Be(PhotoUploadSessionStatus.Failed);
        session.FailureReason.Should().Be("upload verification failed");

        await tracker.MarkExpiredAsync(session.StorageKey);
        session.Status.Should().Be(PhotoUploadSessionStatus.Expired);
        session.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task GetCleanupCandidatesAsync_ReturnsOnlyExpiredPendingOrFailedSessionsOrderedByExpiration()
    {
        await using var db = CreateDbContext("photo-upload-tracker-cleanup");
        var now = DateTimeOffset.UtcNow;
        db.PhotoUploadSessions.AddRange(
            CreateEntity("photos/pending-old.jpg", PhotoUploadSessionStatus.Pending, now.AddMinutes(-30)),
            CreateEntity("photos/failed-old.jpg", PhotoUploadSessionStatus.Failed, now.AddMinutes(-10)),
            CreateEntity("photos/completed-old.jpg", PhotoUploadSessionStatus.Completed, now.AddMinutes(-20)),
            CreateEntity("photos/pending-future.jpg", PhotoUploadSessionStatus.Pending, now.AddMinutes(5)));
        await db.SaveChangesAsync();

        var tracker = new DbPhotoUploadInitTracker(db);

        var candidates = await tracker.GetCleanupCandidatesAsync(now);

        candidates.Select(x => x.StorageKey)
            .Should()
            .Equal("photos/pending-old.jpg", "photos/failed-old.jpg");
    }

    [Test]
    public async Task RemovePendingUploadAsync_SoftDeletesSessionAndUnknownKeysAreIgnored()
    {
        await using var db = CreateDbContext("photo-upload-tracker-remove");
        var session = CreateEntity("photos/remove.jpg", PhotoUploadSessionStatus.Pending, DateTimeOffset.UtcNow.AddMinutes(10));
        db.PhotoUploadSessions.Add(session);
        await db.SaveChangesAsync();

        var tracker = new DbPhotoUploadInitTracker(db);

        await tracker.RemovePendingUploadAsync(session.StorageKey);
        await tracker.MarkFailedAsync("photos/missing.jpg", "ignored");

        session.IsDeleted.Should().BeTrue();
        db.PhotoUploadSessions.IgnoreQueryFilters().Single(x => x.StorageKey == session.StorageKey).IsDeleted.Should().BeTrue();

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        (await tracker.GetUploadSessionAsync(session.StorageKey)).Should().BeNull();
    }

    private static AppDbContext CreateDbContext(string name)
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"{name}-{Id<DbPhotoUploadInitTrackerTests>.New():N}")
            .Options);

    private static PendingPhotoUpload CreatePendingUpload(string storageKey, DateTimeOffset expiresAtUtc)
        => new()
        {
            StorageKey = storageKey,
            InitiatedByUserId = Id<User>.New(),
            OwnerUserId = Id<User>.New(),
            ReportRequestId = Id<ReportRequest>.New(),
            ViewType = PhotoViewType.Front,
            DeclaredContentType = "image/jpeg",
            DeclaredSizeBytes = 1024,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            ExpiresAtUtc = expiresAtUtc,
            Status = PhotoUploadSessionStatus.Pending
        };

    private static PhotoUploadSession CreateEntity(string storageKey, PhotoUploadSessionStatus status, DateTimeOffset expiresAtUtc)
        => new()
        {
            Id = Id<PhotoUploadSession>.New(),
            StorageKey = storageKey,
            InitiatedByUserId = Id<User>.New(),
            OwnerUserId = Id<User>.New(),
            ReportRequestId = Id<ReportRequest>.New(),
            ViewType = PhotoViewType.Front,
            DeclaredContentType = "image/jpeg",
            DeclaredSizeBytes = 1024,
            ExpiresAtUtc = expiresAtUtc,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        };
}
