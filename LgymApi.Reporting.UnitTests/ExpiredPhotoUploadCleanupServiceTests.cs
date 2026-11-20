using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Repositories;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using LgymApi.Identity.Contracts.AccountDeletion;
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
        var tracker = Substitute.For<IReportPhotoPersistence>();
        tracker.ListCleanupCandidatesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var storage = Substitute.For<IPhotoStorageProvider>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var service = CreateService(tracker, storage, unitOfWork);

        var cleaned = await service.CleanupExpiredUploadsAsync();

        Assert.That(cleaned, Is.EqualTo(0));
        await storage.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
        await tracker.DidNotReceiveWithAnyArgs().MarkUploadExpiredAsync(default!, default);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CleanupExpiredUploadsAsync_WhenCandidatesExist_DeletesMarksAndSavesOnce()
    {
        var candidate = CreatePendingUpload("photos/cleanup-1.jpg");
        var tracker = Substitute.For<IReportPhotoPersistence>();
        tracker.ListCleanupCandidatesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([candidate]);

        var storage = Substitute.For<IPhotoStorageProvider>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var service = CreateService(tracker, storage, unitOfWork);

        var cleaned = await service.CleanupExpiredUploadsAsync();

        Assert.That(cleaned, Is.EqualTo(1));
        await storage.Received(1).DeleteAsync(candidate.StorageKey, Arg.Any<CancellationToken>());
        await tracker.Received(1).MarkUploadExpiredAsync(candidate.StorageKey, Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CleanupExpiredUploadsAsync_WhenStorageDeleteFails_ContinuesWithRemainingCandidates()
    {
        var failedCandidate = CreatePendingUpload("photos/fail.jpg");
        var successfulCandidate = CreatePendingUpload("photos/success.jpg");
        var tracker = Substitute.For<IReportPhotoPersistence>();
        tracker.ListCleanupCandidatesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([failedCandidate, successfulCandidate]);

        var storage = Substitute.For<IPhotoStorageProvider>();
        storage.DeleteAsync(failedCandidate.StorageKey, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("storage failure"));

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var logger = Substitute.For<ILogger<ExpiredPhotoUploadCleanupService>>();
        var service = CreateService(tracker, storage, unitOfWork, logger);

        var cleaned = await service.CleanupExpiredUploadsAsync();

        Assert.That(cleaned, Is.EqualTo(1));
        await storage.Received(1).DeleteAsync(failedCandidate.StorageKey, Arg.Any<CancellationToken>());
        await tracker.DidNotReceive().MarkUploadExpiredAsync(failedCandidate.StorageKey, Arg.Any<CancellationToken>());
        await tracker.Received(1).MarkUploadExpiredAsync(successfulCandidate.StorageKey, Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        var warningCall = logger.ReceivedCalls().Single(call =>
            call.GetMethodInfo().Name == nameof(ILogger.Log)
            && call.GetArguments()[0] is LogLevel logLevel
            && logLevel == LogLevel.Warning);
        var warningState = warningCall.GetArguments()[2];
        Assert.That(warningState, Is.Not.Null);
        Assert.That(warningState!.ToString(), Does.Contain(failedCandidate.StorageKey));
        Assert.That(warningCall.GetArguments()[3], Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public async Task CleanupExpiredUploadsAsync_WhenMarkExpiredFails_AbortsWithoutCommitting()
    {
        var candidate = CreatePendingUpload("photos/mark-fail.jpg");
        var tracker = Substitute.For<IReportPhotoPersistence>();
        tracker.ListCleanupCandidatesAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([candidate]);
        tracker.MarkUploadExpiredAsync(candidate.StorageKey, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("tracker failure"));

        var storage = Substitute.For<IPhotoStorageProvider>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var service = CreateService(tracker, storage, unitOfWork);

        var cleanup = () => service.CleanupExpiredUploadsAsync();

        await Assert.ThatAsync(async () => { await cleanup(); }, Throws.InstanceOf<InvalidOperationException>());
        await storage.Received(1).DeleteAsync(candidate.StorageKey, Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static ExpiredPhotoUploadCleanupService CreateService(
        IReportPhotoPersistence tracker,
        IPhotoStorageProvider storage,
        IUnitOfWork unitOfWork,
        ILogger<ExpiredPhotoUploadCleanupService>? logger = null)
        => new(
            tracker,
            storage,
            unitOfWork,
            logger ?? Substitute.For<ILogger<ExpiredPhotoUploadCleanupService>>(),
            Substitute.For<IAccountDeletionSynchronization>(),
            Substitute.For<IAccountDeletionRetention>());

    private static PendingPhotoUpload CreatePendingUpload(string storageKey)
        => new(
            Id<PhotoUploadSession>.New(),
            storageKey,
            Id<AccountReference>.New(),
            Id<AccountReference>.New(),
            Id<ReportRequest>.New(),
            PhotoViewType.Front.ToString(),
            "image/jpeg",
            1024,
            DateTimeOffset.UtcNow.AddMinutes(-20),
            DateTimeOffset.UtcNow.AddMinutes(-5),
            null,
            null,
            PhotoUploadSessionStatus.Pending,
            null);
}
