using FluentAssertions;
using LgymApi.Application.Abstractions.Storage;
using LgymApi.Application.Features.Reporting.Models;
using LgymApi.Application.Reporting.Errors;
using LgymApi.Application.Reporting.Persistence;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;
using NSubstitute;

namespace LgymApi.UnitTests;

public sealed class ReportingPhotoRequestOwnershipTests
{
    [Test]
    public async Task AnotherRelatedTrainer_CannotInitiatePhotoForOriginalTrainersRequest()
    {
        var traineeId = Id<User>.New();
        var request = PhotoServiceTestFactory.CreateReportRequest(Id<ReportRequest>.New(), traineeId);
        var otherTrainer = PhotoServiceTestFactory.CreateUser(Id<User>.New(), "other-trainer@example.test");
        var photos = Substitute.For<IReportPhotoPersistence>();
        var storage = Substitute.For<IPhotoStorageProvider>();
        var service = PhotoServiceTestFactory.CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request),
            relationshipAccess: (_, _, _) => Task.FromResult(true), reportingRepository: photos, photoStorageProvider: storage);

        var result = await service.InitiatePhotoUploadAsync(otherTrainer, new InitiatePhotoUploadCommand
        {
            ReportRequestId = request.Id, ViewType = "Front", MimeType = "image/jpeg", SizeBytes = 100
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ReportingNotFoundError>();
        await photos.DidNotReceiveWithAnyArgs().RecordUploadInitAsync(default!);
        await storage.DidNotReceiveWithAnyArgs().GenerateSignedUploadUrlAsync(default!, default!, default);
    }

    [Test]
    public async Task AnotherRelatedTrainer_CannotCompleteReplaceOrDeleteOriginalTrainersSlot()
    {
        var traineeId = Id<User>.New();
        var otherTrainerId = Id<User>.New();
        var request = PhotoServiceTestFactory.CreateReportRequest(Id<ReportRequest>.New(), traineeId);
        var session = PhotoServiceTestFactory.CreatePendingUpload($"photos/{traineeId}/{request.Id}/Front/test.jpg", otherTrainerId, traineeId, request.Id, "Front", "image/jpeg", 100);
        var photos = Substitute.For<IReportPhotoPersistence>();
        var storage = Substitute.For<IPhotoStorageProvider>();
        var service = PhotoServiceTestFactory.CreateService(
            findRequestById: (_, _) => Task.FromResult<ReportRequest?>(request), relationshipAccess: (_, _, _) => Task.FromResult(true),
            reportingRepository: photos, photoStorageProvider: storage, pendingUpload: session);

        var result = await service.CompletePhotoUploadAsync(PhotoServiceTestFactory.CreateUser(otherTrainerId, "other-trainer@example.test"), new CompletePhotoUploadCommand
        {
            ReportRequestId = request.Id, ViewType = "Front", MimeType = "image/jpeg", SizeBytes = 100, StorageKey = session.StorageKey
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ReportingNotFoundError>();
        await photos.DidNotReceiveWithAnyArgs().SaveAsync(default!);
        await storage.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
        await storage.DidNotReceiveWithAnyArgs().GetMetadataAsync(default!);
    }
}
