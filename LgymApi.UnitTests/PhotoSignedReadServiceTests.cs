using FluentAssertions;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using NSubstitute;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class PhotoSignedReadServiceTests
{
    [Test]
    public async Task GetSignedReadUrlAsync_WhenAuthorizedPhotoExists_ReturnsSignedUrl()
    {
        var ownerId = Id<User>.New();
        var currentUser = PhotoServiceTestFactory.CreateUser(ownerId, "user@example.com");
        var photoId = Id<Photo>.New();
        var photo = new Photo
        {
            Id = photoId, OwnerUserId = ownerId, UploaderUserId = ownerId, ReportRequestId = Id<ReportRequest>.New(),
            ViewType = PhotoViewType.Front.ToString(), StorageKey = "photos/front.jpg", MimeType = "image/jpeg", SizeBytes = 1024, Checksum = "etag"
        };

        var repo = Substitute.For<IReportingRepository>();
        repo.FindPhotoByIdAsync(photoId, Arg.Any<CancellationToken>()).Returns(photo);
        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        storageProvider.GenerateSignedReadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns("https://storage.example.com/read-url");

        var service = PhotoServiceTestFactory.CreateService(reportingRepository: repo, photoStorageProvider: storageProvider);
        var result = await service.GetSignedReadUrlAsync(currentUser, photoId);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReadUrl.Should().Be("https://storage.example.com/read-url");
    }

    [Test]
    public async Task GetSignedReadUrlAsync_WhenPhotoIsDeleted_ReturnsNotFound()
    {
        var ownerId = Id<User>.New();
        var currentUser = PhotoServiceTestFactory.CreateUser(ownerId, "user@example.com");
        var photoId = Id<Photo>.New();
        var repo = Substitute.For<IReportingRepository>();
        repo.FindPhotoByIdAsync(photoId, Arg.Any<CancellationToken>()).Returns((Photo?)null);

        var service = PhotoServiceTestFactory.CreateService(reportingRepository: repo);
        var result = await service.GetSignedReadUrlAsync(currentUser, photoId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ReportingNotFoundError>();
    }

    [Test]
    public async Task GetSignedReadUrlAsync_WhenUnrelatedTraineeRequestsPhoto_ReturnsForbiddenWithoutSignedUrl()
    {
        var ownerId = Id<User>.New();
        var otherUser = PhotoServiceTestFactory.CreateUser(Id<User>.New(), "other@example.com");
        var photoId = Id<Photo>.New();
        var photo = new Photo
        {
            Id = photoId, OwnerUserId = ownerId, UploaderUserId = ownerId, ReportRequestId = Id<ReportRequest>.New(),
            ViewType = PhotoViewType.Front.ToString(), StorageKey = "photos/private-front.jpg", MimeType = "image/jpeg", SizeBytes = 1024, Checksum = "etag"
        };
        var repo = Substitute.For<IReportingRepository>();
        repo.FindPhotoByIdAsync(photoId, Arg.Any<CancellationToken>()).Returns(photo);
        var storageProvider = Substitute.For<IPhotoStorageProvider>();

        var service = PhotoServiceTestFactory.CreateService(reportingRepository: repo, photoStorageProvider: storageProvider);
        var result = await service.GetSignedReadUrlAsync(otherUser, photoId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ReportingForbiddenError>();
        await storageProvider.DidNotReceive().GenerateSignedReadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetSignedReadUrlAsync_WhenAssignedTrainerRequestsTraineePhoto_ReturnsSignedUrl()
    {
        var traineeId = Id<User>.New();
        var trainerId = Id<User>.New();
        var trainer = PhotoServiceTestFactory.CreateUser(trainerId, "trainer@example.com");
        var photoId = Id<Photo>.New();
        var photo = new Photo
        {
            Id = photoId, OwnerUserId = traineeId, UploaderUserId = traineeId, ReportRequestId = Id<ReportRequest>.New(),
            ViewType = PhotoViewType.Front.ToString(), StorageKey = "photos/trainee/front.jpg", MimeType = "image/jpeg", SizeBytes = 1024, Checksum = "etag"
        };
        var repository = Substitute.For<IReportingRepository>();
        repository.FindPhotoByIdAsync(photoId, Arg.Any<CancellationToken>()).Returns(photo);
        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        storageProvider.GenerateSignedReadUrlAsync(photo.StorageKey, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/read-url");
        var service = PhotoServiceTestFactory.CreateService(
            reportingRepository: repository,
            photoStorageProvider: storageProvider,
            userHasRole: (_, _, _) => Task.FromResult(true),
            hasTrainerTraineeLink: (currentTrainerId, currentTraineeId, _) => Task.FromResult<TrainerTraineeLink?>(
                currentTrainerId == trainerId && currentTraineeId == traineeId
                    ? new TrainerTraineeLink { Id = Id<TrainerTraineeLink>.New(), TrainerId = trainerId, TraineeId = traineeId }
                    : null));

        var result = await service.GetSignedReadUrlAsync(trainer, photoId);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReadUrl.Should().Be("https://storage.example.com/read-url");
        await storageProvider.Received(1).GenerateSignedReadUrlAsync(photo.StorageKey, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetSignedReadUrlAsync_WhenNonAssignedTrainerRequestsTraineePhoto_ReturnsNotFoundWithoutSignedUrl()
    {
        var traineeId = Id<User>.New();
        var trainer = PhotoServiceTestFactory.CreateUser(Id<User>.New(), "non-assigned-trainer@example.com");
        var photoId = Id<Photo>.New();
        var photo = new Photo
        {
            Id = photoId, OwnerUserId = traineeId, UploaderUserId = traineeId, ReportRequestId = Id<ReportRequest>.New(),
            ViewType = PhotoViewType.Front.ToString(), StorageKey = "photos/trainee/private-front.jpg", MimeType = "image/jpeg", SizeBytes = 1024, Checksum = "etag"
        };
        var repository = Substitute.For<IReportingRepository>();
        repository.FindPhotoByIdAsync(photoId, Arg.Any<CancellationToken>()).Returns(photo);
        var storageProvider = Substitute.For<IPhotoStorageProvider>();
        var service = PhotoServiceTestFactory.CreateService(
            reportingRepository: repository,
            photoStorageProvider: storageProvider,
            userHasRole: (_, _, _) => Task.FromResult(true),
            hasTrainerTraineeLink: (_, _, _) => Task.FromResult<TrainerTraineeLink?>(null));

        var result = await service.GetSignedReadUrlAsync(trainer, photoId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ReportingNotFoundError>();
        await storageProvider.DidNotReceive().GenerateSignedReadUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetSignedReadUrlAsync_WhenPhotoIdIsEmpty_ReturnsResourceBackedInvalidError()
    {
        var currentUser = PhotoServiceTestFactory.CreateUser(Id<User>.New(), "user@example.com");
        var service = PhotoServiceTestFactory.CreateService();

        var result = await service.GetSignedReadUrlAsync(currentUser, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidReportingError>();
        result.Error.Message.Should().Be(Messages.FieldRequired);
    }
}
