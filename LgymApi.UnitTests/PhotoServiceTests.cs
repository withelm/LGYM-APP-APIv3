using FluentAssertions;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Features.Reporting;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Options;
using LgymApi.Application.Repositories;
using LgymApi.BackgroundWorker.Common;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.Security;
using LgymApi.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class PhotoServiceTests
{
    [Test]
    public async Task InitiatePhotoUploadAsync_WhenTraineeOwnsRequest_ReturnsSuccessWithStorageKeyAndUrl()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var currentUser = CreateUser(traineeId, "trainee@example.com");
        var request = CreateReportRequest(requestId, traineeId);

        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        storageProvider.GenerateSignedUploadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/signed-upload-url");

        var service = CreateService(
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
    public async Task InitiatePhotoUploadAsync_WhenAssignedTrainerAccessesTraineeRequest_ReturnsSuccess()
    {
        var traineeId = Id<User>.New();
        var trainerId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var trainer = CreateUser(trainerId, "trainer@example.com");
        var request = CreateReportRequest(requestId, traineeId);
        var link = new TrainerTraineeLink
        {
            Id = Id<TrainerTraineeLink>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId
        };

        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        storageProvider.GenerateSignedUploadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/signed-upload-url");

        var service = CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            userHasRole: (_, _, _) => Task.FromResult(true),
            hasTrainerTraineeLink: (tId, trainee, _) => Task.FromResult(tId == trainerId && trainee == traineeId ? link : null),
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
        var trainer = CreateUser(trainerId, "non-assigned-trainer@example.com");
        var request = CreateReportRequest(requestId, traineeId);

        var service = CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            userHasRole: (_, _, _) => Task.FromResult(true),
            hasTrainerTraineeLink: (_, _, _) => Task.FromResult<TrainerTraineeLink?>(null));

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

    [Test]
    [TestCase("video/mp4")]
    [TestCase("application/pdf")]
    [TestCase("image/gif")]
    [TestCase("image/bmp")]
    public async Task InitiatePhotoUploadAsync_WhenInvalidMimeType_ReturnsInvalidError(string mimeType)
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var currentUser = CreateUser(traineeId, "trainee@example.com");
        var request = CreateReportRequest(requestId, traineeId);

        var service = CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request));

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

    [Test]
    [TestCase("image/jpeg")]
    [TestCase("image/png")]
    [TestCase("image/heic")]
    public async Task InitiatePhotoUploadAsync_WhenValidMimeType_ReturnsSuccess(string mimeType)
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var currentUser = CreateUser(traineeId, "trainee@example.com");
        var request = CreateReportRequest(requestId, traineeId);

        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        storageProvider.GenerateSignedUploadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/signed-upload-url");

        var service = CreateService(
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
        var currentUser = CreateUser(traineeId, "trainee@example.com");
        var request = CreateReportRequest(requestId, traineeId);

        var service = CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request));

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
        var currentUser = CreateUser(traineeId, "trainee@example.com");
        var request = CreateReportRequest(requestId, traineeId);

        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        storageProvider.GenerateSignedUploadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/signed-upload-url");

        var service = CreateService(
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
    public async Task GetSignedReadUrlAsync_WhenAuthorizedPhotoExists_ReturnsSignedUrl()
    {
        var ownerId = Id<User>.New();
        var currentUser = CreateUser(ownerId, "user@example.com");
        var photoId = Id<Photo>.New();
        var photo = new Photo
        {
            Id = photoId,
            OwnerUserId = ownerId,
            UploaderUserId = ownerId,
            ReportRequestId = Id<ReportRequest>.New(),
            ViewType = PhotoViewType.Front,
            StorageKey = "photos/front.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 1024,
            Checksum = "etag"
        };

        var repo = Substitute.For<IReportingRepository>();
        repo.FindPhotoByIdAsync(photoId, Arg.Any<CancellationToken>()).Returns(photo);

        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        storageProvider.GenerateSignedReadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/read-url");

        var service = CreateService(reportingRepository: repo, photoStorageProvider: storageProvider);

        var result = await service.GetSignedReadUrlAsync(currentUser, photoId.ToString());

        result.IsSuccess.Should().BeTrue();
        result.Value.ReadUrl.Should().Be("https://storage.example.com/read-url");
    }

    [Test]
    public async Task GetSignedReadUrlAsync_WhenPhotoIsDeleted_ReturnsNotFound()
    {
        var ownerId = Id<User>.New();
        var currentUser = CreateUser(ownerId, "user@example.com");
        var photoId = Id<Photo>.New();

        var repo = Substitute.For<IReportingRepository>();
        repo.FindPhotoByIdAsync(photoId, Arg.Any<CancellationToken>()).Returns((Photo?)null);

        var service = CreateService(reportingRepository: repo);

        var result = await service.GetSignedReadUrlAsync(currentUser, photoId.ToString());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ReportingNotFoundError>();
    }

    [Test]
    public async Task CompletePhotoUploadAsync_WhenDuplicateFinalize_ShouldSoftDeleteOldPhoto()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var currentUser = CreateUser(traineeId, "trainee@example.com");
        var request = CreateReportRequest(requestId, traineeId);

        var oldPhoto = new Photo
        {
            Id = Id<Photo>.New(),
            ReportRequestId = requestId,
            OwnerUserId = traineeId,
            UploaderUserId = traineeId,
            ViewType = PhotoViewType.Front,
            StorageKey = "photos/old-front.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 1024,
            Checksum = "oldchecksum",
            IsDeleted = false
        };

        var existingPhotos = new List<Photo> { oldPhoto };
        Photo? savedPhoto = null;

        var repo = Substitute.For<IReportingRepository>();
        repo.FindRequestByIdAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(request);
        repo.GetPhotosByRequestIdAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(existingPhotos);
        repo.FindActivePhotoByRequestAndViewAsync(requestId, PhotoViewType.Front, Arg.Any<CancellationToken>())
            .Returns(oldPhoto);
        repo.SavePhotoAsync(Arg.Do<Photo>(p =>
            {
                savedPhoto = p;
                oldPhoto.IsDeleted = true;
                oldPhoto.UpdatedAt = DateTimeOffset.UtcNow;
            }), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        storageProvider.GetMetadataAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PhotoMetadata
            {
                ContentType = "image/jpeg",
                SizeBytes = 2048,
                ETag = "newchecksum",
                UploadedAt = DateTimeOffset.UtcNow
            });

        var pendingUpload = new PendingPhotoUpload
        {
            StorageKey = $"photos/{traineeId}/{requestId}/Front/new-photo.jpg",
            InitiatedByUserId = traineeId,
            OwnerUserId = traineeId,
            ReportRequestId = requestId,
            ViewType = "Front",
            MimeType = "image/jpeg",
            SizeBytes = 2048,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(10)
        };

        var service = CreateService(reportingRepository: repo, photoStorageProvider: storageProvider, pendingUpload: pendingUpload);

        var result = await service.CompletePhotoUploadAsync(currentUser, new CompletePhotoUploadCommand
        {
            ReportRequestId = requestId,
            ViewType = "Front",
            StorageKey = $"photos/{traineeId}/{requestId}/Front/new-photo.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 2048,
            Checksum = "newchecksum"
        });

        result.IsSuccess.Should().BeTrue();
        oldPhoto.IsDeleted.Should().BeTrue();
        oldPhoto.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        savedPhoto.Should().NotBeNull();
        savedPhoto!.ViewType.Should().Be(PhotoViewType.Front);
        savedPhoto.Checksum.Should().Be("newchecksum");
    }

    [Test]
    public async Task CompletePhotoUploadAsync_WhenMetadataDoesNotMatchClientSize_ReturnsInvalidError()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var currentUser = CreateUser(traineeId, "trainee@example.com");
        var request = CreateReportRequest(requestId, traineeId);

        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        storageProvider.GetMetadataAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PhotoMetadata
            {
                ContentType = "image/jpeg",
                SizeBytes = 4096,
                ETag = "etag",
                UploadedAt = DateTimeOffset.UtcNow
            });

        var pendingUpload = new PendingPhotoUpload
        {
            StorageKey = $"photos/{traineeId}/{requestId}/Front/test.jpg",
            InitiatedByUserId = traineeId,
            OwnerUserId = traineeId,
            ReportRequestId = requestId,
            ViewType = "Front",
            MimeType = "image/jpeg",
            SizeBytes = 2048,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(10)
        };

        var service = CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            photoStorageProvider: storageProvider,
            pendingUpload: pendingUpload);

        var result = await service.CompletePhotoUploadAsync(currentUser, new CompletePhotoUploadCommand
        {
            ReportRequestId = requestId,
            ViewType = "Front",
            StorageKey = $"photos/{traineeId}/{requestId}/Front/test.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 2048,
            Checksum = "etag"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidReportingError>();
        result.Error.Message.Should().Contain("size");
    }

    [Test]
    public async Task CompletePhotoUploadAsync_WhenPendingUploadMissing_ReturnsInvalidError()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var currentUser = CreateUser(traineeId, "trainee@example.com");
        var request = CreateReportRequest(requestId, traineeId);

        var service = CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request));

        var result = await service.CompletePhotoUploadAsync(currentUser, new CompletePhotoUploadCommand
        {
            ReportRequestId = requestId,
            ViewType = "Front",
            StorageKey = $"photos/{traineeId}/{requestId}/Front/test.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 2048,
            Checksum = "etag"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidReportingError>();
        result.Error.Message.Should().Contain("Upload session");
    }

    [Test]
    public async Task CompletePhotoUploadAsync_WhenNumericViewTypeIsUndefined_ReturnsInvalidError()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var currentUser = CreateUser(traineeId, "trainee@example.com");
        var request = CreateReportRequest(requestId, traineeId);

        var service = CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request));

        var result = await service.CompletePhotoUploadAsync(currentUser, new CompletePhotoUploadCommand
        {
            ReportRequestId = requestId,
            ViewType = "999",
            StorageKey = $"photos/{traineeId}/{requestId}/Front/test.jpg",
            MimeType = "image/jpeg",
            SizeBytes = 2048,
            Checksum = "etag"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidReportingError>();
        result.Error.Message.Should().Contain("Invalid view type");
    }

    [Test]
    public async Task GetPhotoHistoryAsync_WhenNonOwnerAndNonTrainer_ReturnsForbidden()
    {
        var traineeId = Id<User>.New();
        var otherUserId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var otherUser = CreateUser(otherUserId, "other@example.com");
        var request = CreateReportRequest(requestId, traineeId);

        var service = CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            userHasRole: (_, _, _) => Task.FromResult(false));

        var result = await service.GetPhotoHistoryAsync(otherUser, new GetPhotoHistoryCommand
        {
            TraineeId = traineeId,
            RequestId = requestId
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ReportingForbiddenError>();
    }

    [Test]
    public async Task GetPhotoHistoryAsync_WhenNonAssignedTrainer_ReturnsForbidden()
    {
        var traineeId = Id<User>.New();
        var trainerId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var trainer = CreateUser(trainerId, "non-assigned-trainer@example.com");
        var request = CreateReportRequest(requestId, traineeId);

        var service = CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            userHasRole: (_, _, _) => Task.FromResult(true),
            hasTrainerTraineeLink: (_, _, _) => Task.FromResult<TrainerTraineeLink?>(null));

        var result = await service.GetPhotoHistoryAsync(trainer, new GetPhotoHistoryCommand
        {
            TraineeId = traineeId,
            RequestId = requestId
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ReportingNotFoundError>();
    }

    [Test]
    public async Task GetPhotoHistoryAsync_WhenOwner_ReturnsHistory()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var trainee = CreateUser(traineeId, "trainee@example.com");
        var request = CreateReportRequest(requestId, traineeId);

        var photos = new List<Photo>
        {
            new Photo
            {
                Id = Id<Photo>.New(),
                ReportRequestId = requestId,
                OwnerUserId = traineeId,
                UploaderUserId = traineeId,
                ViewType = PhotoViewType.Front,
                StorageKey = "photos/front.jpg",
                MimeType = "image/jpeg",
                SizeBytes = 1024,
                Checksum = "abc123",
                IsDeleted = false
            }
        };

        var repo = Substitute.For<IReportingRepository>();
        repo.GetPhotosByRequestIdAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(photos);

        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        storageProvider.GenerateSignedReadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/read-url");

        var service = CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            reportingRepository: repo,
            photoStorageProvider: storageProvider);

        var result = await service.GetPhotoHistoryAsync(trainee, new GetPhotoHistoryCommand
        {
            TraineeId = traineeId,
            RequestId = requestId
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].ViewType.Should().Be("Front");
    }

    [Test]
    public async Task GetPhotoHistoryAsync_WhenAssignedTrainer_ReturnsHistory()
    {
        var traineeId = Id<User>.New();
        var trainerId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var trainer = CreateUser(trainerId, "trainer@example.com");
        var request = CreateReportRequest(requestId, traineeId);
        var link = new TrainerTraineeLink
        {
            Id = Id<TrainerTraineeLink>.New(),
            TrainerId = trainerId,
            TraineeId = traineeId
        };

        var photos = new List<Photo>
        {
            new Photo
            {
                Id = Id<Photo>.New(),
                ReportRequestId = requestId,
                OwnerUserId = traineeId,
                UploaderUserId = traineeId,
                ViewType = PhotoViewType.Side,
                StorageKey = "photos/side.jpg",
                MimeType = "image/jpeg",
                SizeBytes = 2048,
                Checksum = "def456",
                IsDeleted = false
            }
        };

        var repo = Substitute.For<IReportingRepository>();
        repo.GetPhotosByRequestIdAsync(requestId, Arg.Any<CancellationToken>())
            .Returns(photos);

        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        storageProvider.GenerateSignedReadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/read-url");

        var service = CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            userHasRole: (_, _, _) => Task.FromResult(true),
            hasTrainerTraineeLink: (tId, trainee, _) => Task.FromResult(tId == trainerId && trainee == traineeId ? link : null),
            reportingRepository: repo,
            photoStorageProvider: storageProvider);

        var result = await service.GetPhotoHistoryAsync(trainer, new GetPhotoHistoryCommand
        {
            TraineeId = traineeId,
            RequestId = requestId
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].ViewType.Should().Be("Side");
    }

    [Test]
    public async Task BackwardCompatibility_ExistingScalarTests_StillPass()
    {
        var traineeId = Id<User>.New();
        var requestId = Id<ReportRequest>.New();
        var currentUser = CreateUser(traineeId, "trainee@example.com");
        var request = CreateReportRequest(requestId, traineeId);

        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        storageProvider.GenerateSignedUploadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/signed-upload-url");

        var service = CreateService(
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

    private static User CreateUser(Id<User> id, string email) => new()
    {
        Id = id,
        Name = "Test User",
        Email = email,
        ProfileRank = "Rookie"
    };

    private static ReportRequest CreateReportRequest(Id<ReportRequest> id, Id<User> traineeId) => new()
    {
        Id = id,
        TraineeId = traineeId,
        TrainerId = Id<User>.New(),
        TemplateId = Id<ReportTemplate>.New(),
        Status = ReportRequestStatus.Pending,
        IsDeleted = false
    };

    private static IReportingService CreateService(
        Func<Id<ReportRequest>, CancellationToken, Task<ReportRequest?>>? findRequestById = null,
        Func<Id<User>, string, CancellationToken, Task<bool>>? userHasRole = null,
        Func<Id<User>, Id<User>, CancellationToken, Task<TrainerTraineeLink?>>? hasTrainerTraineeLink = null,
        IPhotoStorageProvider? photoStorageProvider = null,
        IReportingRepository? reportingRepository = null,
        PendingPhotoUpload? pendingUpload = null)
    {
        var roleRepository = Substitute.For<IRoleRepository>();
        roleRepository.UserHasRoleAsync(Arg.Any<Id<User>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => userHasRole?.Invoke(ci.ArgAt<Id<User>>(0), ci.ArgAt<string>(1), ci.ArgAt<CancellationToken>(2)) ?? Task.FromResult(false));

        var trainerRelationshipRepository = Substitute.For<ITrainerRelationshipRepository>();
        trainerRelationshipRepository.FindActiveLinkByTrainerAndTraineeAsync(Arg.Any<Id<User>>(), Arg.Any<Id<User>>(), Arg.Any<CancellationToken>())
            .Returns(ci => hasTrainerTraineeLink?.Invoke(ci.ArgAt<Id<User>>(0), ci.ArgAt<Id<User>>(1), ci.ArgAt<CancellationToken>(2)) ?? Task.FromResult<TrainerTraineeLink?>(null));

        var repo = reportingRepository ?? Substitute.For<IReportingRepository>();
        if (findRequestById != null)
        {
            repo.FindRequestByIdAsync(Arg.Any<Id<ReportRequest>>(), Arg.Any<CancellationToken>())
                .Returns(ci => findRequestById(ci.ArgAt<Id<ReportRequest>>(0), ci.ArgAt<CancellationToken>(1)));
        }

        var commandDispatcher = Substitute.For<ICommandDispatcher>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var storageProvider = photoStorageProvider ?? Substitute.For<IPhotoStorageProvider>();
        var uploadInitTracker = Substitute.For<IPhotoUploadInitTracker>();
        uploadInitTracker.CountRecentUploadInitsAsync(Arg.Any<Id<User>>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(0);
        uploadInitTracker.GetPendingUploadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var storageKey = ci.ArgAt<string>(0);
                return pendingUpload != null && string.Equals(pendingUpload.StorageKey, storageKey, StringComparison.Ordinal)
                    ? pendingUpload
                    : null;
            });

        var dependencies = Substitute.For<IReportingServiceDependencies>();
        dependencies.RoleRepository.Returns(roleRepository);
        dependencies.TrainerRelationshipRepository.Returns(trainerRelationshipRepository);
        dependencies.ReportingRepository.Returns(repo);
        dependencies.CommandDispatcher.Returns(commandDispatcher);
        dependencies.UnitOfWork.Returns(unitOfWork);
        dependencies.PhotoStorageProvider.Returns(storageProvider);
        dependencies.PhotoUploadInitTracker.Returns(uploadInitTracker);
        dependencies.Logger.Returns(Substitute.For<ILogger<ReportingService>>());
        dependencies.PhotoStorageOptions.Returns(new PhotoStorageOptions());

        return new ReportingService(dependencies);
    }
}
