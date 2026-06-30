using FluentAssertions;
using LgymApi.Application.Features.Reporting;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using LgymApi.Infrastructure.Services;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class InMemoryPhotoUploadInitTrackerTests
{
    [Test]
    public async Task RecordUploadInitAsync_CountRecentUploadInitsAsync_TracksOnlyEntriesWithinWindow()
    {
        var tracker = new InMemoryPhotoUploadInitTracker();
        var userId = Id<User>.New();

        await tracker.RecordUploadInitAsync(CreatePendingUpload(userId, "photos/old.jpg", DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddMinutes(5)));
        await tracker.RecordUploadInitAsync(CreatePendingUpload(userId, "photos/recent.jpg", DateTimeOffset.UtcNow.AddMinutes(-20), DateTimeOffset.UtcNow.AddMinutes(5)));

        var count = await tracker.CountRecentUploadInitsAsync(userId, DateTimeOffset.UtcNow.AddHours(-1));

        count.Should().Be(1);
    }

    [Test]
    public async Task GetUploadSessionAsync_WhenUploadExpired_ReturnsStoredExpiredSession()
    {
        var tracker = new InMemoryPhotoUploadInitTracker();
        var userId = Id<User>.New();
        var pendingUpload = CreatePendingUpload(userId, "photos/expired.jpg", DateTimeOffset.UtcNow.AddMinutes(-20), DateTimeOffset.UtcNow.AddMinutes(-1));
        await tracker.RecordUploadInitAsync(pendingUpload);

        var result = await tracker.GetUploadSessionAsync(pendingUpload.StorageKey);
        var secondRead = await tracker.GetUploadSessionAsync(pendingUpload.StorageKey);

        result.Should().NotBeNull();
        result!.ExpiresAtUtc.Should().BeBefore(DateTimeOffset.UtcNow);
        secondRead.Should().NotBeNull();
    }

    [Test]
    public async Task RemovePendingUploadAsync_RemovesRecordedUpload()
    {
        var tracker = new InMemoryPhotoUploadInitTracker();
        var userId = Id<User>.New();
        var pendingUpload = CreatePendingUpload(userId, "photos/remove.jpg", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10));
        await tracker.RecordUploadInitAsync(pendingUpload);

        await tracker.RemovePendingUploadAsync(pendingUpload.StorageKey);

        (await tracker.GetUploadSessionAsync(pendingUpload.StorageKey)).Should().BeNull();
    }

    [Test]
    public async Task MarkCompletedAsync_UpdatesStoredPendingUpload()
    {
        var tracker = new InMemoryPhotoUploadInitTracker();
        var userId = Id<User>.New();
        var pendingUpload = CreatePendingUpload(userId, "photos/complete.jpg", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10));
        var photoId = Id<Photo>.New();
        var completedAt = DateTimeOffset.UtcNow;
        await tracker.RecordUploadInitAsync(pendingUpload);

        await tracker.MarkCompletedAsync(pendingUpload.StorageKey, photoId, completedAt);

        var session = await tracker.GetUploadSessionAsync(pendingUpload.StorageKey);
        session.Should().NotBeNull();
        session!.Status.Should().Be(LgymApi.Domain.Enums.PhotoUploadSessionStatus.Completed);
        session.CompletedPhotoId.Should().Be(photoId);
        session.CompletedAtUtc.Should().Be(completedAt);
    }

    [Test]
    public async Task MarkFailedAsync_AndGetCleanupCandidates_ReturnsOnlyFailedOrPendingExpiredUploads()
    {
        var tracker = new InMemoryPhotoUploadInitTracker();
        var userId = Id<User>.New();
        var failed = CreatePendingUpload(userId, "photos/failed.jpg", DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddMinutes(-5));
        var completed = CreatePendingUpload(userId, "photos/completed.jpg", DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddMinutes(-5));
        var pending = CreatePendingUpload(userId, "photos/pending.jpg", DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddMinutes(-5));
        await tracker.RecordUploadInitAsync(failed);
        await tracker.RecordUploadInitAsync(completed);
        await tracker.RecordUploadInitAsync(pending);
        await tracker.MarkFailedAsync(failed.StorageKey, "checksum mismatch");
        await tracker.MarkCompletedAsync(completed.StorageKey, Id<Photo>.New(), DateTimeOffset.UtcNow);

        var cleanupCandidates = await tracker.GetCleanupCandidatesAsync(DateTimeOffset.UtcNow);

        cleanupCandidates.Select(x => x.StorageKey).Should().BeEquivalentTo([failed.StorageKey, pending.StorageKey]);
        (await tracker.GetUploadSessionAsync(failed.StorageKey))!.FailureReason.Should().Be("checksum mismatch");
    }

    [Test]
    public async Task MarkExpiredAsync_UpdatesStoredStatus()
    {
        var tracker = new InMemoryPhotoUploadInitTracker();
        var userId = Id<User>.New();
        var pendingUpload = CreatePendingUpload(userId, "photos/expired-status.jpg", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10));
        await tracker.RecordUploadInitAsync(pendingUpload);

        await tracker.MarkExpiredAsync(pendingUpload.StorageKey);

        var session = await tracker.GetUploadSessionAsync(pendingUpload.StorageKey);
        session.Should().NotBeNull();
        session!.Status.Should().Be(LgymApi.Domain.Enums.PhotoUploadSessionStatus.Expired);
    }

    private static PendingPhotoUpload CreatePendingUpload(Id<User> userId, string storageKey, DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc)
        => new()
        {
            StorageKey = storageKey,
            InitiatedByUserId = userId,
            OwnerUserId = userId,
            ReportRequestId = Id<ReportRequest>.New(),
            ViewType = LgymApi.Domain.Enums.PhotoViewType.Front.ToString(),
            DeclaredContentType = "image/jpeg",
            DeclaredSizeBytes = 1024,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc
        };
}
