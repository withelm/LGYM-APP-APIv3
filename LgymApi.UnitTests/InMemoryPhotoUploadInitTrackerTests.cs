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
    public async Task GetPendingUploadAsync_WhenUploadExpired_RemovesAndReturnsNull()
    {
        var tracker = new InMemoryPhotoUploadInitTracker();
        var userId = Id<User>.New();
        var pendingUpload = CreatePendingUpload(userId, "photos/expired.jpg", DateTimeOffset.UtcNow.AddMinutes(-20), DateTimeOffset.UtcNow.AddMinutes(-1));
        await tracker.RecordUploadInitAsync(pendingUpload);

        var result = await tracker.GetPendingUploadAsync(pendingUpload.StorageKey);
        var secondRead = await tracker.GetPendingUploadAsync(pendingUpload.StorageKey);

        result.Should().BeNull();
        secondRead.Should().BeNull();
    }

    [Test]
    public async Task RemovePendingUploadAsync_RemovesRecordedUpload()
    {
        var tracker = new InMemoryPhotoUploadInitTracker();
        var userId = Id<User>.New();
        var pendingUpload = CreatePendingUpload(userId, "photos/remove.jpg", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10));
        await tracker.RecordUploadInitAsync(pendingUpload);

        await tracker.RemovePendingUploadAsync(pendingUpload.StorageKey);

        (await tracker.GetPendingUploadAsync(pendingUpload.StorageKey)).Should().BeNull();
    }

    private static PendingPhotoUpload CreatePendingUpload(Id<User> userId, string storageKey, DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc)
        => new()
        {
            StorageKey = storageKey,
            InitiatedByUserId = userId,
            OwnerUserId = userId,
            ReportRequestId = Id<ReportRequest>.New(),
            ViewType = "Front",
            MimeType = "image/jpeg",
            SizeBytes = 1024,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc
        };
}
