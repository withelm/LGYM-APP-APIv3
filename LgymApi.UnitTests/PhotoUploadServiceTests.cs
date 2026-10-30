using FluentAssertions;
using System.Text.Json;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Repositories;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class PhotoUploadServiceTests
{
    [Test]
    public async Task InitiatePhotoUploadAsync_WithSchema2Snapshot_PinsCanonicalTypeAndSlotWithoutDictionaryLookup()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var photoTypeId = Id<TrainerPhotoType>.New();
        var slotId = Id<PhotoUploadSession>.New().ToString();
        var currentUser = PhotoServiceTestFactory.CreateUser(traineeId, "trainee@example.com");
        var request = PhotoServiceTestFactory.CreateReportRequest(requestId, traineeId);
        request.DefinitionSnapshot = CreateSnapshot(request, photoTypeId, slotId);
        PendingPhotoUpload? recorded = null;
        var photos = Substitute.For<IReportPhotoPersistence>();
        photos.RecordUploadInitAsync(Arg.Do<PendingPhotoUpload>(session => recorded = session), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        storageProvider.GenerateSignedUploadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/upload");
        var service = PhotoServiceTestFactory.CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            reportingRepository: photos,
            photoStorageProvider: storageProvider);

        var result = await service.InitiatePhotoUploadAsync(currentUser, new InitiatePhotoUploadCommand
        {
            ReportRequestId = requestId,
            ViewType = "Front",
            PhotoTypeId = photoTypeId,
            ReportPhotoSlotId = slotId,
            MimeType = "image/jpeg",
            SizeBytes = 1024
        });

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : string.Empty);
        recorded.Should().NotBeNull();
        recorded!.SourcePhotoTypeId.Should().Be(photoTypeId);
        recorded.ReportPhotoSlotId.Should().Be(slotId);
        recorded.ViewType.Should().Be("Front");
    }

    [Test]
    public async Task InitiatePhotoUploadAsync_WithSchema2SnapshotAndMismatchedTypeSlot_IsRejected()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var photoTypeId = Id<TrainerPhotoType>.New();
        var request = PhotoServiceTestFactory.CreateReportRequest(requestId, traineeId);
        request.DefinitionSnapshot = CreateSnapshot(request, photoTypeId, Id<PhotoUploadSession>.New().ToString());
        var service = PhotoServiceTestFactory.CreateService(findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request));

        var result = await service.InitiatePhotoUploadAsync(
            PhotoServiceTestFactory.CreateUser(traineeId, "trainee@example.com"),
            new InitiatePhotoUploadCommand
            {
                ReportRequestId = requestId,
                ViewType = "Front",
                PhotoTypeId = photoTypeId,
                ReportPhotoSlotId = Id<PhotoUploadSession>.New().ToString(),
                MimeType = "image/jpeg",
                SizeBytes = 1024
            });

        result.IsFailure.Should().BeTrue();
    }

    [TestCase(false, "Front", true)]
    [TestCase(true, "Back", false)]
    [TestCase(true, "Front", true)]
    public async Task InitiatePhotoUploadAsync_ResolvesLegacyViewAndRejectsConflictingDualIdentity(bool includeIdentity, string view, bool expectedSuccess)
    {
        var traineeId = Id<User>.New();
        var request = PhotoServiceTestFactory.CreateReportRequest(Id<ReportRequest>.New(), traineeId);
        var typeId = Id<TrainerPhotoType>.New();
        var slotId = Id<PhotoUploadSession>.New().ToString();
        request.DefinitionSnapshot = CreateSnapshot(request, typeId, slotId);
        var photos = Substitute.For<IReportPhotoPersistence>();
        PendingPhotoUpload? captured = null;
        photos.RecordUploadInitAsync(Arg.Do<PendingPhotoUpload>(value => captured = value), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var service = PhotoServiceTestFactory.CreateService(findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request), reportingRepository: photos);
        var result = await service.InitiatePhotoUploadAsync(PhotoServiceTestFactory.CreateUser(traineeId, "trainee@example.com"), new InitiatePhotoUploadCommand
        {
            ReportRequestId = request.Id, ViewType = view, PhotoTypeId = includeIdentity ? typeId : null,
            ReportPhotoSlotId = includeIdentity ? slotId : null, MimeType = "image/jpeg", SizeBytes = 1024
        });
        result.IsSuccess.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            captured.Should().NotBeNull();
            captured!.SourcePhotoTypeId.Should().Be(typeId);
            captured.ReportPhotoSlotId.Should().Be(slotId);
        }
        else captured.Should().BeNull();
    }

    [Test]
    public async Task InitiatePhotoUploadAsync_WhenTraineeOwnsRequest_ReturnsSuccessWithStorageKeyAndUrl()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var currentUser = PhotoServiceTestFactory.CreateUser(traineeId, "trainee@example.com");
        var request = PhotoServiceTestFactory.CreateReportRequest(requestId, traineeId);

        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        storageProvider.GenerateSignedUploadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/signed-upload-url");

        var service = PhotoServiceTestFactory.CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            photoStorageProvider: storageProvider);

        var result = await service.InitiatePhotoUploadAsync(currentUser, new InitiatePhotoUploadCommand
        {
            ReportRequestId = requestId,
            ViewType = "front",
            MimeType = "image/jpeg",
            SizeBytes = 5_242_880
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.UploadUrl.Should().Be("https://storage.example.com/signed-upload-url");
        result.Value.StorageKey.Should().Contain("photos/");
        result.Value.StorageKey.Should().Contain(traineeId.ToString());
        result.Value.StorageKey.Should().Contain(requestId.ToString());
        result.Value.StorageKey.Should().Contain("Front");
        result.Value.StorageKey.Should().EndWith(".jpg");
        result.Value.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Test]
    public async Task InitiatePhotoUploadAsync_PersistsPendingUploadSession()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var currentUser = PhotoServiceTestFactory.CreateUser(traineeId, "trainee@example.com");
        var request = PhotoServiceTestFactory.CreateReportRequest(requestId, traineeId);

        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        storageProvider.GenerateSignedUploadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/signed-upload-url");

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var uploadInitTracker = Substitute.For<IReportPhotoPersistence>();
        PendingPhotoUpload? recordedSession = null;
        uploadInitTracker.RecordUploadInitAsync(
                Arg.Do<PendingPhotoUpload>(session => recordedSession = session),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var service = PhotoServiceTestFactory.CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            photoStorageProvider: storageProvider,
            unitOfWork: unitOfWork,
            photoUploadInitTracker: uploadInitTracker);

        var result = await service.InitiatePhotoUploadAsync(currentUser, new InitiatePhotoUploadCommand
        {
            ReportRequestId = requestId,
            ViewType = "front",
            MimeType = "image/jpeg",
            SizeBytes = 5_242_880
        });

        result.IsSuccess.Should().BeTrue();
        recordedSession.Should().NotBeNull();
        recordedSession!.StorageKey.Should().Be(result.Value.StorageKey);
        recordedSession.InitiatedByAccountId.Should().Be(currentUser.Id);
        recordedSession.OwnerAccountId.Should().Be(ReportingTestData.AccountId(traineeId));
        recordedSession.ReportRequestId.Should().Be(requestId);
        recordedSession.ViewType.Should().Be(PhotoViewType.Front.ToString());
        recordedSession.DeclaredContentType.Should().Be("image/jpeg");
        recordedSession.DeclaredSizeBytes.Should().Be(5_242_880);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InitiatePhotoUploadAsync_WhenAssignedTrainerAccessesTraineeRequest_ReturnsSuccess()
    {
        var traineeId = Id<User>.New();
        var trainerId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var trainer = PhotoServiceTestFactory.CreateUser(trainerId, "trainer@example.com");
        var request = PhotoServiceTestFactory.CreateReportRequest(requestId, traineeId);
        request.TrainerId = trainerId;
        request.Template.TrainerId = trainerId;
        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        storageProvider.GenerateSignedUploadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/signed-upload-url");

        var service = PhotoServiceTestFactory.CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            relationshipAccess: (currentTrainerId, currentTraineeId, _) => Task.FromResult(
                currentTrainerId == trainerId && currentTraineeId == traineeId),
            photoStorageProvider: storageProvider);

        var result = await service.InitiatePhotoUploadAsync(trainer, new InitiatePhotoUploadCommand
        {
            ReportRequestId = requestId,
            ViewType = "side",
            MimeType = "image/png",
            SizeBytes = 3_145_728
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.StorageKey.Should().Contain("Side");
        result.Value.StorageKey.Should().EndWith(".png");
    }

    [Test]
    public async Task InitiatePhotoUploadAsync_WhenNonAssignedTrainerAccessesRequest_ReturnsForbidden()
    {
        var traineeId = Id<User>.New();
        var trainerId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var trainer = PhotoServiceTestFactory.CreateUser(trainerId, "non-assigned-trainer@example.com");
        var request = PhotoServiceTestFactory.CreateReportRequest(requestId, traineeId);

        var service = PhotoServiceTestFactory.CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            relationshipAccess: (_, _, _) => Task.FromResult(false));

        var result = await service.InitiatePhotoUploadAsync(trainer, new InitiatePhotoUploadCommand
        {
            ReportRequestId = requestId,
            ViewType = "front",
            MimeType = "image/jpeg",
            SizeBytes = 2_097_152
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ReportingNotFoundError>();
    }

    [TestCase("video/mp4")]
    [TestCase("application/pdf")]
    [TestCase("image/gif")]
    [TestCase("image/bmp")]
    public async Task InitiatePhotoUploadAsync_WhenInvalidMimeType_ReturnsInvalidError(string mimeType)
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var currentUser = PhotoServiceTestFactory.CreateUser(traineeId, "trainee@example.com");
        var request = PhotoServiceTestFactory.CreateReportRequest(requestId, traineeId);

        var service = PhotoServiceTestFactory.CreateService(findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request));

        var result = await service.InitiatePhotoUploadAsync(currentUser, new InitiatePhotoUploadCommand
        {
            ReportRequestId = requestId,
            ViewType = "front",
            MimeType = mimeType,
            SizeBytes = 2_097_152
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidReportingError>();
        result.Error.Message.Should().Contain("Invalid MIME type");
    }

    [TestCase("image/jpeg")]
    [TestCase("image/png")]
    [TestCase("image/heic")]
    public async Task InitiatePhotoUploadAsync_WhenValidMimeType_ReturnsSuccess(string mimeType)
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var currentUser = PhotoServiceTestFactory.CreateUser(traineeId, "trainee@example.com");
        var request = PhotoServiceTestFactory.CreateReportRequest(requestId, traineeId);

        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        storageProvider.GenerateSignedUploadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/signed-upload-url");

        var service = PhotoServiceTestFactory.CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            photoStorageProvider: storageProvider);

        var result = await service.InitiatePhotoUploadAsync(currentUser, new InitiatePhotoUploadCommand
        {
            ReportRequestId = requestId,
            ViewType = "front",
            MimeType = mimeType,
            SizeBytes = 2_097_152
        });

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task InitiatePhotoUploadAsync_WhenFileSizeExceedsConfiguredLimit_ReturnsInvalidError()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var currentUser = PhotoServiceTestFactory.CreateUser(traineeId, "trainee@example.com");
        var request = PhotoServiceTestFactory.CreateReportRequest(requestId, traineeId);

        var service = PhotoServiceTestFactory.CreateService(findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request));

        var result = await service.InitiatePhotoUploadAsync(currentUser, new InitiatePhotoUploadCommand
        {
            ReportRequestId = requestId,
            ViewType = "front",
            MimeType = "image/jpeg",
            SizeBytes = 5_242_881
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidReportingError>();
        result.Error.Message.Should().Contain("File size exceeds maximum");
    }

    [Test]
    public async Task InitiatePhotoUploadAsync_WhenFileSizeWithinConfiguredLimit_ReturnsSuccess()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var currentUser = PhotoServiceTestFactory.CreateUser(traineeId, "trainee@example.com");
        var request = PhotoServiceTestFactory.CreateReportRequest(requestId, traineeId);

        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        storageProvider.GenerateSignedUploadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/signed-upload-url");

        var service = PhotoServiceTestFactory.CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            photoStorageProvider: storageProvider);

        var result = await service.InitiatePhotoUploadAsync(currentUser, new InitiatePhotoUploadCommand
        {
            ReportRequestId = requestId,
            ViewType = "front",
            MimeType = "image/jpeg",
            SizeBytes = 5_242_880
        });

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task BackwardCompatibility_ExistingScalarTests_StillPass()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var currentUser = PhotoServiceTestFactory.CreateUser(traineeId, "trainee@example.com");
        var request = PhotoServiceTestFactory.CreateReportRequest(requestId, traineeId);

        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        storageProvider.GenerateSignedUploadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/signed-upload-url");

        var service = PhotoServiceTestFactory.CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            photoStorageProvider: storageProvider);

        var result = await service.InitiatePhotoUploadAsync(currentUser, new InitiatePhotoUploadCommand
        {
            ReportRequestId = requestId,
            ViewType = "front",
            MimeType = "image/jpeg",
            SizeBytes = 5_242_880
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.UploadUrl.Should().Be("https://storage.example.com/signed-upload-url");
        result.Value.StorageKey.Should().Contain("photos/");
    }

    [Test]
    public async Task InitiatePhotoUploadAsync_WhenRequestAlreadySubmitted_ReturnsInvalidError()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var currentUser = PhotoServiceTestFactory.CreateUser(traineeId, "trainee@example.com");
        var request = PhotoServiceTestFactory.CreateReportRequest(requestId, traineeId);
        request.Status = ReportRequestStatus.Submitted;

        var service = PhotoServiceTestFactory.CreateService(findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request));

        var result = await service.InitiatePhotoUploadAsync(currentUser, new InitiatePhotoUploadCommand
        {
            ReportRequestId = requestId,
            ViewType = "front",
            MimeType = "image/jpeg",
            SizeBytes = 2_097_152
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidReportingError>();
        result.Error.Message.Should().Be(LgymApi.Resources.Messages.ReportRequestNotPending);
    }

    [Test]
    public async Task InitiatePhotoUploadAsync_WhenProviderIsNotLocal_SkipsDevelopmentLimits()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var currentUser = PhotoServiceTestFactory.CreateUser(traineeId, "trainee@example.com");
        var request = PhotoServiceTestFactory.CreateReportRequest(requestId, traineeId);
        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        storageProvider.GenerateSignedUploadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/signed-upload-url");
        var reportingRepository = Substitute.For<IReportPhotoPersistence>();
        reportingRepository.GetActiveStorageBytesAsync(Arg.Any<CancellationToken>()).Returns(long.MaxValue);

        var service = PhotoServiceTestFactory.CreateService(
            photoStorageProvider: storageProvider,
            reportingRepository: reportingRepository,
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            photoStorageOptions: new LgymApi.Application.Options.PhotoStorageOptions { Provider = "CloudflareR2" });

        var result = await service.InitiatePhotoUploadAsync(currentUser, new InitiatePhotoUploadCommand
        {
            ReportRequestId = requestId,
            ViewType = "front",
            MimeType = "image/jpeg",
            SizeBytes = 2_097_152
        });

        result.IsSuccess.Should().BeTrue();
    }

    private static ReportRequestDefinitionSnapshot CreateSnapshot(
        ReportRequest request,
        Id<TrainerPhotoType> photoTypeId,
        string slotId)
    {
        var moduleConfig = JsonSerializer.SerializeToElement(new
        {
            schemaVersion = 2,
            requiredPhotoTypeIds = new[] { photoTypeId.ToString() },
            photoTypes = new[]
            {
                new
                {
                    photoTypeId = photoTypeId.ToString(),
                    reportPhotoSlotId = slotId,
                    systemKey = "Front",
                    nameMode = PhotoTypeNameMode.SystemLocalized,
                    labels = new { pl = "Przód", en = "Front" }
                }
            }
        });
        return new ReportRequestDefinitionSnapshot
        {
            Id = Id<ReportRequestDefinitionSnapshot>.New(),
            ReportRequestId = request.Id,
            SourceTemplateId = request.TemplateId,
            DefinitionJson = JsonSerializer.Serialize(new ReportTemplateSnapshotDocument(
                request.TemplateId.ToString(),
                request.Template.Name,
                null,
                [new ReportTemplateSnapshotFieldDocument("photos", "Photos", ReportFieldType.Photos, true, 1, moduleConfig)],
                "Issued")),
            Provenance = "Issued",
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
