using FluentAssertions;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Repositories;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
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
        var result = await service.GetSignedReadUrlAsync(currentUser, photoId.ToString());

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
        var result = await service.GetSignedReadUrlAsync(currentUser, photoId.ToString());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ReportingNotFoundError>();
    }

    [Test]
    public async Task GetSignedReadUrlAsync_WhenPhotoIdMissingOrInvalid_ReturnsInvalidError()
    {
        var currentUser = PhotoServiceTestFactory.CreateUser(Id<User>.New(), "user@example.com");
        var service = PhotoServiceTestFactory.CreateService();

        var missingResult = await service.GetSignedReadUrlAsync(currentUser, " ");
        var invalidResult = await service.GetSignedReadUrlAsync(currentUser, "not-a-guid");

        missingResult.IsFailure.Should().BeTrue();
        invalidResult.IsFailure.Should().BeTrue();
        missingResult.Error.Should().BeOfType<InvalidReportingError>();
        invalidResult.Error.Should().BeOfType<InvalidReportingError>();
    }
}
