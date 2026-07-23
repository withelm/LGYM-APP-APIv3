using FluentAssertions;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ExpiredPhotoUploadCleanupServiceTests
{
    [Test]
    public async Task CleanupExpiredUploadsAsync_WhenNoCandidates_ReturnsZeroWithoutSaving()
    {
        var tracker = Substitute.For<IPhotoUploadInitTracker>();
        tracker.GetCleanupCandidatesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var storage = Substitute.For<IPhotoStorageProvider>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var service = CreateService(tracker, storage, unitOfWork);

        var cleaned = await service.CleanupExpiredUploadsAsync();

        cleaned.Should().Be(0);
        await storage.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
        await tracker.DidNotReceiveWithAnyArgs().MarkExpiredAsync(default!, default);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CleanupExpiredUploadsAsync_WhenCandidatesExist_DeletesMarksAndSavesOnce()
    {
        var candidate = CreatePendingUpload("photos/cleanup-1.jpg");
        var tracker = Substitute.For<IPhotoUploadInitTracker>();
        tracker.GetCleanupCandidatesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([candidate]);

        var storage = Substitute.For<IPhotoStorageProvider>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var service = CreateService(tracker, storage, unitOfWork);

        var cleaned = await service.CleanupExpiredUploadsAsync();

        cleaned.Should().Be(1);
        await storage.Received(1).DeleteAsync(candidate.StorageKey, Arg.Any<CancellationToken>());
        await tracker.Received(1).MarkExpiredAsync(candidate.StorageKey, Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CleanupExpiredUploadsAsync_WhenStorageDeleteFails_ContinuesWithRemainingCandidates()
    {
        var failedCandidate = CreatePendingUpload("photos/fail.jpg");
        var successfulCandidate = CreatePendingUpload("photos/success.jpg");
        var tracker = Substitute.For<IPhotoUploadInitTracker>();
        tracker.GetCleanupCandidatesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([failedCandidate, successfulCandidate]);

        var storage = Substitute.For<IPhotoStorageProvider>();
        storage.DeleteAsync(failedCandidate.StorageKey, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("storage failure"));

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var logger = Substitute.For<ILogger<ExpiredPhotoUploadCleanupService>>();
        var service = CreateService(tracker, storage, unitOfWork, logger);

        var cleaned = await service.CleanupExpiredUploadsAsync();

        cleaned.Should().Be(1);
        await storage.Received(1).DeleteAsync(failedCandidate.StorageKey, Arg.Any<CancellationToken>());
        await tracker.DidNotReceive().MarkExpiredAsync(failedCandidate.StorageKey, Arg.Any<CancellationToken>());
        await tracker.Received(1).MarkExpiredAsync(successfulCandidate.StorageKey, Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        var warningCall = logger.ReceivedCalls().Single(call =>
            call.GetMethodInfo().Name == nameof(ILogger.Log)
            && call.GetArguments()[0] is LogLevel logLevel
            && logLevel == LogLevel.Warning);
        var warningState = warningCall.GetArguments()[2];
        warningState.Should().NotBeNull();
        warningState!.ToString().Should().Contain(failedCandidate.StorageKey);
        warningCall.GetArguments()[3].Should().BeOfType<InvalidOperationException>();
    }

    [Test]
    public async Task CleanupExpiredUploadsAsync_WhenMarkExpiredFails_DoesNotCountCandidateAsCleaned()
    {
        var candidate = CreatePendingUpload("photos/mark-fail.jpg");
        var tracker = Substitute.For<IPhotoUploadInitTracker>();
        tracker.GetCleanupCandidatesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([candidate]);
        tracker.MarkExpiredAsync(candidate.StorageKey, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("tracker failure"));

        var storage = Substitute.For<IPhotoStorageProvider>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var service = CreateService(tracker, storage, unitOfWork);

        var cleaned = await service.CleanupExpiredUploadsAsync();

        cleaned.Should().Be(0);
        await storage.Received(1).DeleteAsync(candidate.StorageKey, Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static ExpiredPhotoUploadCleanupService CreateService(
        IPhotoUploadInitTracker tracker,
        IPhotoStorageProvider storage,
        IUnitOfWork unitOfWork,
        ILogger<ExpiredPhotoUploadCleanupService>? logger = null)
        => new(
            tracker,
            storage,
            unitOfWork,
            logger ?? Substitute.For<ILogger<ExpiredPhotoUploadCleanupService>>());

    private static PendingPhotoUpload CreatePendingUpload(string storageKey)
        => new()
        {
            StorageKey = storageKey,
            InitiatedByUserId = Domain.ValueObjects.Id<Domain.Entities.User>.New(),
            OwnerUserId = Domain.ValueObjects.Id<Domain.Entities.User>.New(),
            ReportRequestId = Domain.ValueObjects.Id<Domain.Entities.ReportRequest>.New(),
            ViewType = Domain.Enums.PhotoViewType.Front.ToString(),
            DeclaredContentType = "image/jpeg",
            DeclaredSizeBytes = 1024,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-20),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            Status = Domain.Enums.PhotoUploadSessionStatus.Pending
        };
}
